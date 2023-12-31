﻿using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;

namespace SubNPhoenix
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = sender as Hyperlink;
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;    //不使用shell启动
                p.StartInfo.RedirectStandardInput = true;//喊cmd接受标准输入
                p.StartInfo.RedirectStandardOutput = false;//不想听cmd讲话所以不要他输出
                p.StartInfo.RedirectStandardError = true;//重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;//不显示窗口
                p.Start();//向cmd窗口发送输入信息 后面的&exit告诉cmd运行好之后就退出
                p.StandardInput.WriteLine("start " + link.NavigateUri.AbsoluteUri + "&exit");
                p.StandardInput.AutoFlush = true;
                p.WaitForExit();
                p.Close();
            }
        }
    }
}

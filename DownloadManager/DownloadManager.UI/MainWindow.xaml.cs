using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DownloadManager.Core.Classes;
using DownloadManager.Core.Enums;

namespace DownloadManager.UI
{
    public partial class MainWindow : Window
    {
        DownloadsManager manager;

        public MainWindow()
        {
            manager = new DownloadsManager()
            {
                Downloads = new List<Downloader>()
            };
            Downloader d = new Downloader("http://www.sample-videos.com/video/mp4/720/big_buck_bunny_720p_10mb.mp4")
            {
                Folder = @"E:\TestDownloadsFolder"
            };
            manager.Downloads.Add(d);

            Downloader d1 = new Downloader("http://download.microsoft.com/download/9/5/A/95A9616B-7A37-4AF6-BC36-D6EA96C8DAAE/dotNetFx40_Full_x86_x64.exe")
            {
                Folder = @"E:\TestDownloadsFolder"
            };
            manager.Downloads.Add(d1);
            InitializeComponent();
            downloadsGrid.ItemsSource = manager.Downloads;
        }

        private void downloadButtonClick(object sender, RoutedEventArgs e)
        {
            if (manager.Downloads[downloadsGrid.SelectedIndex].Status == DownloadStatus.Paused)
                manager.Downloads[downloadsGrid.SelectedIndex].Resume();
            else
                manager.Downloads[downloadsGrid.SelectedIndex].Download();
            pauseButton.IsEnabled = true;
        }

        private void addButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void pauseButtonClick(object sender, RoutedEventArgs e)
        {
            manager.Downloads[downloadsGrid.SelectedIndex].Pause();
            pauseButton.IsEnabled = false;
        }

        private void deleteButtonClick(object sender, RoutedEventArgs e)
        {
            manager.Downloads[downloadsGrid.SelectedIndex].Cancel();
            manager.Downloads.RemoveAt(downloadsGrid.SelectedIndex);
        }

        private void dataGridConvertTime(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            if (e.PropertyName == "TotalUsedTime")
            {
                DataGridTextColumn column = e.Column as DataGridTextColumn;
                Binding binding = column.Binding as Binding;
                binding.StringFormat = "hh:mm:ss";
            }
        }
    }
}

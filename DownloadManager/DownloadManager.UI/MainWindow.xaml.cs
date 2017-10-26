using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Collections.Generic;
using DownloadManager.Core.Enums;
using DownloadManager.Core.Classes;

namespace DownloadManager.UI
{
    public partial class MainWindow : Window
    {
        DownloadsManager manager;

        public MainWindow()
        {
            manager = new DownloadsManager()
            {
                Downloads = new List<Component>()
            };
            Downloader d = new Downloader("http://www.sample-videos.com/video/mp4/720/big_buck_bunny_720p_10mb.mp4")
            {
                Folder = @"E:\TestDownloadsFolder"
            };
            manager.Add(d);

            Downloader d1 = new Downloader("http://download.microsoft.com/download/9/5/A/95A9616B-7A37-4AF6-BC36-D6EA96C8DAAE/dotNetFx40_Full_x86_x64.exe")
            {
                Folder = @"E:\TestDownloadsFolder"
            };
            manager.Add(d1);
            InitializeComponent();
            downloadsGrid.ItemsSource = manager.Downloads;
        }

        private void downloadButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                manager.CurrentIndex = downloadsGrid.SelectedIndex;
                manager.Download();
                pauseButton.IsEnabled = true;
            }
            catch { }
            
        }

        private void addButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void pauseButtonClick(object sender, RoutedEventArgs e)
        {
            manager.Pause();
            pauseButton.IsEnabled = false;
        }

        private void deleteButtonClick(object sender, RoutedEventArgs e)
        {
            manager.Cancel();
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

        private void downloadsGridSelectionChanged(object sender, RoutedEventArgs e)
        {
            manager.CurrentIndex = downloadsGrid.SelectedIndex;
        }
    }
}

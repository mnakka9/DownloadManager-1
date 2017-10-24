using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadManager.Core.Classes
{
    public class DownloadsManager
    {
        public List<Downloader> Downloads = new List<Downloader>();

        public DownloadsManager() : this(new List<Downloader>()) { }

        public DownloadsManager(List<Downloader> downloads)
        {
            Downloads = downloads;
        }
    }
}

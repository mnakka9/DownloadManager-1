using System;
using System.Net;
using DownloadManager.Core.Enums;

namespace DownloadManager.Core.Interfaces
{
    public interface IDownloader
    {
        #region Basic settings of a WebDownloader.

        Uri Url { get; }
        string DownloadPath { get; set; }
        long TotalSize { get; set; }

        ICredentials Credentials { get; set; }
        IWebProxy Proxy { get; set; }

        #endregion


        #region Support the "Pause", "Resume" and Multi-Threads feature.

        bool IsRangeSupported { get; set; }
        long StartPoint { get; set; }
        long EndPoint { get; set; }

        #endregion

        #region The downloaded data and status.

        long DownloadedSize { get; }
        int CachedSize { get; }

        bool HasChecked { get; set; }
        DownloadStatus Status { get; }
        TimeSpan TotalUsedTime { get; }

        #endregion

        #region Advanced settings of a WebDownloader

        int BufferSize { get; set; }
        int BufferCountPerNotification { get; set; }
        int MaxCacheSize { get; set; }

        #endregion

        void CheckUrl(out string fileName);
        
        void Download();

        void Resume();
    }
}

using System;
using System.IO;

namespace DownloadManager.Core.DownloadEventArgs
{
    public class DownloadCompletedEventArgs: EventArgs
    {
        public long DownloadedSize { get; private set; }
        public long TotalSize { get; private set; }
        public Exception Error { get; private set; }
        public TimeSpan TotalTime { get; private set; }
        public FileInfo DownloadedFile { get; private set; }

        public DownloadCompletedEventArgs(FileInfo downloadedFile, long downloadedSize, long totalSize, TimeSpan totalTime, Exception ex)
        {
            DownloadedFile = downloadedFile;
            DownloadedSize = downloadedSize;
            TotalSize = totalSize;
            TotalTime = totalTime;
            Error = ex;
        }
    }
}

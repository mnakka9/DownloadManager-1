using System;

namespace DownloadManager.Core.DownloadEventArgs
{
    public class DownloadProgressChangedEventArgs: EventArgs
    {
        public long ReceivedSize { get; private set; }
        public long TotalSize { get; private set; }
        public int DownloadSpeed { get; private set; }

        public DownloadProgressChangedEventArgs(long receivedSize, long totalSize, int downloadSpeed)
        {
            ReceivedSize = receivedSize;
            TotalSize = totalSize;
            DownloadSpeed = downloadSpeed;
        }
    }
}

using DownloadManager.Core.Enums;

namespace DownloadManager.Core.Interfaces
{
    public interface IObserver
    {
        DownloadStatus Status { get; }
        void UpdateStatus(DownloadStatus status);
        void Run();
        void Resume();
    }
}

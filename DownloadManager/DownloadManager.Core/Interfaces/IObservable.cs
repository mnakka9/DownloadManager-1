using DownloadManager.Core.Enums;

namespace DownloadManager.Core.Interfaces
{
    public interface IObservable
    {
        void AddObserver(IObserver o);
        void UpdateObservers(DownloadStatus status);
        void RunObservers();
        void ResumeObservers();
    }
}

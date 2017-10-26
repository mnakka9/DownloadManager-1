using System.Collections.Generic;

namespace DownloadManager.Core.Classes
{
    public class DownloadsManager: Component
    {
        public List<Component> Downloads = new List<Component>();

        public Component CurrentDownloader { get; set; }

        public DownloadsManager() : this(new List<Component>()) { }

        public DownloadsManager(List<Component> downloads)
        {
            Downloads = downloads;
        }

        public override void Download()
        {
            if (CurrentDownloader != null)
                CurrentDownloader.Download();
        }

        public override void Pause()
        {
            if (CurrentDownloader != null)
                CurrentDownloader.Pause();
        }

        public override void Resume()
        {
            if (CurrentDownloader != null)
                CurrentDownloader.Resume();
        }

        public override void Cancel()
        {
            if (CurrentDownloader != null)
                CurrentDownloader.Cancel();
        }

        public override void Add(Component d)
        {
            Downloads.Add(d);
        }

        public override void Remove(int index)
        {
            Downloads.RemoveAt(index);
        }
    }
}

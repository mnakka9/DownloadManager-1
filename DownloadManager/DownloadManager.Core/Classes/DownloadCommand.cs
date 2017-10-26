using DownloadManager.Core.Interfaces;

namespace DownloadManager.Core.Classes
{
    class DownloadCommand: ICommand
    {
        private Component downloader;

        public DownloadCommand(Component d)
        {
            downloader = d;
        }

        public void Execute()
        {
            downloader.Download();
        }

        public void Undo()
        {
            downloader.Cancel();
        }
    }
}

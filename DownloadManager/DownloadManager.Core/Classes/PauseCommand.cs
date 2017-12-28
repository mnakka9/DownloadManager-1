using DownloadManager.Core.Interfaces;

namespace DownloadManager.Core.Classes
{
    class PauseCommand: ICommand
    {
        private Component downloader;

        public PauseCommand(Component d)
        {
            downloader = d;
        }

        public void Execute()
        {
            downloader.Pause();
        }

        public void Undo()
        {
            downloader.Resume();
        }
    }
}

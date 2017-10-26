using DownloadManager.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

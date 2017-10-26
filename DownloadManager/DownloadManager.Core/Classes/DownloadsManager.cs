using System.Collections.Generic;
using DownloadManager.Core.Interfaces;

namespace DownloadManager.Core.Classes
{
    public class DownloadsManager: Component
    {
        List<CommandManager> commandManagers = new List<CommandManager>();

        public List<Component> Downloads = new List<Component>();

        public ICommand Command { get; set; }

        public int CurrentIndex { get; set; }

        public DownloadsManager() : this(new List<Component>()) { }

        public DownloadsManager(List<Component> downloads)
        {
            Downloads = downloads;
            foreach(var d in downloads)
            {
                CommandManager cm = new CommandManager();
                cm.AddCommand(new DownloadCommand(d));
                cm.AddCommand(new PauseCommand(d));
                commandManagers.Add(cm);                
            }
                
        }
                
        public override void Download()
        {
            if ((Downloads[CurrentIndex] as Downloader).Status == Enums.DownloadStatus.Paused)
                commandManagers[CurrentIndex].Undo();
            else
                commandManagers[CurrentIndex].RunCommand(0);
        }

        public override void Pause()
        {
            commandManagers[CurrentIndex].RunCommand(1);
        }

        public override void Resume()
        {
            commandManagers[CurrentIndex].Undo();
        }

        public override void Cancel()
        {
            if ((Downloads[CurrentIndex] as Downloader).Status == Enums.DownloadStatus.Paused)
                return;
            commandManagers[CurrentIndex].Undo();
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

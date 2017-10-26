namespace DownloadManager.Core.Classes
{
    public abstract class Component
    {
        public abstract void Download();
        public abstract void Pause();
        public abstract void Cancel();
        public virtual void Resume() { }
        public virtual void Add(Component c) { }
        public virtual void Remove(int index) { }
    }
}

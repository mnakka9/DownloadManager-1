using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using DownloadManager.Core.Enums;
using DownloadManager.Core.Interfaces;
using DownloadManager.Core.DownloadEventArgs;

namespace DownloadManager.Core.Classes
{
    public class Downloader : Component, IDownloader, INotifyPropertyChanged, IObservable
    {
        #region Fields and Properties

        static object locker = new object();
                
        public Uri Url { get; private set; }

        public Thread DownloadThread { get; set; }

        public ICredentials Credentials { get; set; }
        
        public bool IsRangeSupported { get; set; }
        
        public long TotalSize { get; set; }
        
        public long StartPoint { get; set; }
        
        public long EndPoint { get; set; }
        
        public string Folder { get; set; }

        public string DownloadPath
        {
            get
            {
                return Folder + @"\" + Filename;
            }
            set { }
        }

        public string Filename
        {
            get
            {
                return Url.AbsolutePath.Split('/').Last();
            }
        }
        
        public IWebProxy Proxy { get; set; }
        
        public long DownloadedSize
        {
            get
            {
                return downloadClients.Sum(client => (client as HttpDownloadClient).DownloadedSize);
            }
        }

        public string SizeString
        {
            get
            {
                return StringsFormatter.FormatSizeString(DownloadedSize);
            }
        }

        public string SpeedString
        {
            get
            {
                return StringsFormatter.FormatSpeedString(Speed);
            }
        }

        public string TotalUsedTimeString
        {
            get
            {
                return StringsFormatter.FormatTimeSpanString(TotalUsedTime);
            }
        }        

        public int CachedSize
        {
            get
            {
                return downloadClients.Sum(client => (client as HttpDownloadClient).CachedSize);
            }
        }

        public int Progress
        {
            get
            {
                if (TotalSize != 0)
                    return Convert.ToInt32(DownloadedSize * 100 / TotalSize);
                else
                    return 0;
            }
        }

        public DateTime LastUpdateTime { get; set; }

        private TimeSpan usedTime = new TimeSpan();

        private DateTime lastStartTime;
        
        public TimeSpan TotalUsedTime
        {
            get
            {
                if (Status != DownloadStatus.Downloading)
                    return usedTime;
                else
                    return usedTime.Add(DateTime.Now - lastStartTime);
            }
        }
        
        private DateTime lastNotificationTime;
        private long lastNotificationDownloadedSize;

        public int Speed { get; set; }
        
        public int BufferCountPerNotification { get; set; }
        
        public int BufferSize { get; set; }
        
        public int MaxCacheSize { get; set; }

        DownloadStatus status;        
        public DownloadStatus Status
        {
            get
            { return status; }

            private set
            {
                if (status != value)
                {
                    status = value;
                    OnStatusChanged(EventArgs.Empty);
                }
            }
        }
        
        public int MaxThreadCount { get; set; }

        public bool HasChecked { get; set; }
        
        List<IObserver> downloadClients = null;

        public int DownloadThreadsCount
        {
            get
            {
                if (downloadClients != null)
                    return downloadClients.Count;
                else
                    return 0;
            }
        }

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion        

        #region Constructors

        public Downloader(string url) : this(url, 1024, 1048576, 64, Environment.ProcessorCount * 2)
        {
        }

        public Downloader(string url, int bufferSize, int cacheSize, int bufferCountPerNotification, int maxThreadCount)
        {

            Url = new Uri(url);
            StartPoint = 0;
            EndPoint = long.MaxValue;
            BufferSize = bufferSize;
            MaxCacheSize = cacheSize;
            BufferCountPerNotification = bufferCountPerNotification;            

            MaxThreadCount = maxThreadCount;
            
            ServicePointManager.DefaultConnectionLimit = maxThreadCount;
            
            downloadClients = new List<IObserver>();

            Status = DownloadStatus.Initialized;
        }

        #endregion

        #region Methods

        public void CheckUrlAndFile(out string fileName)
        {
            CheckUrl(out fileName);
            CheckFileOrCreateFile();

            HasChecked = true;
        }
        
        public void CheckUrl(out string fileName)
        {
            fileName = DownloaderHelper.CheckUrl(this);
        }
        
        void CheckFileOrCreateFile()
        {
            DownloaderHelper.CheckFileOrCreateFile(this, locker);
        }

        void EnsurePropertyValid()
        {
            if (StartPoint < 0)
                throw new ArgumentOutOfRangeException("StartPoint cannot be less then 0. ");

            if (EndPoint < StartPoint)
                throw new ArgumentOutOfRangeException("EndPoint cannot be less then StartPoint ");

            if (BufferSize < 0)
                throw new ArgumentOutOfRangeException("BufferSize cannot be less then 0. ");

            if (MaxCacheSize < BufferSize)
                throw new ArgumentOutOfRangeException("MaxCacheSize cannot be less then BufferSize ");

            if (BufferCountPerNotification <= 0)
                throw new ArgumentOutOfRangeException("BufferCountPerNotification cannot be less then 0. ");

            if (MaxThreadCount < 1)
                throw new ArgumentOutOfRangeException("maxThreadCount cannot be less than 1. ");
        }
        
        public override void Download()
        {
            if (Status != DownloadStatus.Initialized)
                throw new ApplicationException("Only Initialized download client can be started.");

            Status = DownloadStatus.Waiting;

            LastUpdateTime = DateTime.Now;
            DownloadThread = new Thread(new ThreadStart(DownloadInternal))
            {
                IsBackground = true
            };
            DownloadThread.Start();            
        }

        void DownloadInternal()
        {

            if (Status != DownloadStatus.Waiting)
                return;

            try
            {
                EnsurePropertyValid();

                Status = DownloadStatus.Downloading;

                if (!HasChecked)
                    CheckUrlAndFile(out string filename);

                if (!IsRangeSupported)
                {

                    HttpDownloadClient client = new HttpDownloadClient(Url.AbsoluteUri, 0, long.MaxValue, BufferSize,
                        BufferCountPerNotification * BufferSize, BufferCountPerNotification)
                    {
                        TotalSize = TotalSize,
                        DownloadPath = DownloadPath,
                        HasChecked = true,
                        Credentials = Credentials,
                        Proxy = Proxy
                    };
                    client.DownloadProgressChanged += client_DownloadProgressChanged;
                    client.StatusChanged += client_StatusChanged;
                    client.DownloadCompleted += client_DownloadCompleted;

                    AddObserver(client);
                }
                else
                {
                    int maxSizePerThread = (int)Math.Ceiling((double)TotalSize / MaxThreadCount);

                    if (maxSizePerThread < MaxCacheSize)
                        maxSizePerThread = MaxCacheSize;

                    long leftSizeToDownload = TotalSize;
              
                    int threadsCount = (int)Math.Ceiling((double)TotalSize / maxSizePerThread);

                    for (int i = 0; i < threadsCount; i++)
                    {
                        long endPoint = maxSizePerThread * (i + 1) - 1;
                        long sizeToDownload = maxSizePerThread;

                        if (endPoint > TotalSize)
                        {
                            endPoint = TotalSize - 1;
                            sizeToDownload = endPoint - maxSizePerThread * i;
                        }

                        HttpDownloadClient client = new HttpDownloadClient(Url.AbsoluteUri, maxSizePerThread * i, endPoint)
                        {
                            DownloadPath = DownloadPath,
                            HasChecked = true,
                            TotalSize = sizeToDownload,
                            Credentials = Credentials,
                            Proxy = Proxy
                        };
                        client.DownloadProgressChanged += client_DownloadProgressChanged;
                        client.StatusChanged += client_StatusChanged;
                        client.DownloadCompleted += client_DownloadCompleted;

                        AddObserver(client);
                    }
                }
                
                lastStartTime = DateTime.Now;
                RunObservers();
            }
            catch (Exception ex)
            {
                Cancel();
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
            }
        }
        
        public override void Pause()
        {
            if (Status != DownloadStatus.Downloading)
                throw new ApplicationException("Only downloading downloader can be paused.");

            Status = DownloadStatus.Pausing;

            UpdateObservers(DownloadStatus.Paused);
        }
        
        public override void Resume()
        {
            if (Status != DownloadStatus.Paused)
                throw new ApplicationException("Only paused downloader can be resumed. ");

            lastStartTime = DateTime.Now;
            
            Status = DownloadStatus.Waiting;
            
            foreach (var client in downloadClients)
                if (client.Status != DownloadStatus.Completed)
                    client.Resume();
        }        
        
        public override void Cancel()
        {
            if (Status == DownloadStatus.Initialized || Status == DownloadStatus.Waiting || Status == DownloadStatus.Completed
                || Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled)
            {
                UpdateObservers(DownloadStatus.Canceled);
            }
            else if (Status == DownloadStatus.Canceling || Status == DownloadStatus.Pausing || Status == DownloadStatus.Downloading)
                UpdateObservers(DownloadStatus.Canceling);

            

        }

        private void UpgradeProperties()
        {
            if (DateTime.Now > LastUpdateTime.AddSeconds(1))
            {
                OnPropertyChanged("SpeedString");
                OnPropertyChanged("TotalUsedTimeString");
                LastUpdateTime = DateTime.Now;
            }
            OnPropertyChanged("SizeString");
            OnPropertyChanged("Progress");
        }

        public void AddObserver(IObserver observer)
        {
            downloadClients.Add(observer);
        }

        public void UpdateObservers(DownloadStatus status)
        {
            foreach (var o in downloadClients)
                o.UpdateStatus(status);
        }

        public void RunObservers()
        {
            foreach (var o in downloadClients)
                o.Run();
        }

        public void ResumeObservers()
        {
            foreach (var o in downloadClients)
                o.Resume();
        }
        
        #endregion

        #region EventHandlers

        void client_StatusChanged(object sender, EventArgs e)
        {
            if (downloadClients.All(client => client.Status == DownloadStatus.Completed))
                Status = DownloadStatus.Completed;            
            else if (downloadClients.All(client => client.Status == DownloadStatus.Canceled))
                Status = DownloadStatus.Canceled;
            else
            {
                var nonCompletedClients = downloadClients.Where(client => client.Status != DownloadStatus.Completed);

                if (nonCompletedClients.All(client => client.Status == DownloadStatus.Waiting))
                    Status = DownloadStatus.Waiting;
                else if (nonCompletedClients.All(client => client.Status == DownloadStatus.Paused))
                    Status = DownloadStatus.Paused;
                else if (Status != DownloadStatus.Pausing && Status != DownloadStatus.Canceling)
                    Status = DownloadStatus.Downloading;
            }

        }

        void client_DownloadProgressChanged(object sender, DownloadEventArgs.DownloadProgressChangedEventArgs e)
        {
            lock (locker)
            {
                int speed = 0;
                DateTime current = DateTime.Now;
                TimeSpan interval = current - lastNotificationTime;

                if (interval.TotalSeconds < 60)
                {
                    speed = (int)Math.Floor((DownloadedSize + CachedSize - lastNotificationDownloadedSize) / interval.TotalSeconds);
                    Speed = speed;                    
                }
                    

                lastNotificationTime = current;
                lastNotificationDownloadedSize = DownloadedSize + CachedSize;

                var downloadProgressChangedEventArgs = new DownloadEventArgs.DownloadProgressChangedEventArgs(DownloadedSize, TotalSize, speed);
                                   
                UpgradeProperties();
            }
        }
        
        void client_DownloadCompleted(object sender, DownloadCompletedEventArgs e)
        {
            if (e.Error != null && Status != DownloadStatus.Canceled)
            {
                Cancel();
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, e.Error));
            }
        }
        
        protected virtual void OnStatusChanged(EventArgs e)
        {
            if (Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled || Status == DownloadStatus.Completed)
                usedTime += DateTime.Now - lastStartTime;
            
            if (Status == DownloadStatus.Canceled)
            {
                Exception ex = new Exception("Downloading is canceled by user's request. ");
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
            }

            if (Status == DownloadStatus.Completed)
                OnDownloadCompleted(new DownloadCompletedEventArgs(new FileInfo(DownloadPath), DownloadedSize, TotalSize, TotalUsedTime, null));

            OnPropertyChanged("Status");
        }
        
        protected virtual void OnDownloadCompleted(DownloadCompletedEventArgs e)
        {
            OnPropertyChanged("Progress");
            OnPropertyChanged("Status");
            OnPropertyChanged("SpeedString");
            OnPropertyChanged("DownloadedSize");
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

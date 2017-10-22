using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using DownloadManager.Core.Enums;
using DownloadManager.Core.Interfaces;
using DownloadManager.Core.DownloadEventArgs;

namespace DownloadManager.Core.Classes
{
    class Downloader : IDownloader
    {
        #region Fields and Properties

        static object locker = new object();
                
        public Uri Url { get; private set; }

        public ICredentials Credentials { get; set; }
        
        public bool IsRangeSupported { get; set; }
        
        public long TotalSize { get; set; }
        
        public long StartPoint { get; set; }
        
        public long EndPoint { get; set; }
        
        public string DownloadPath { get; set; }
        
        public IWebProxy Proxy { get; set; }
        
        public long DownloadedSize
        {
            get
            {
                return downloadClients.Sum(client => client.DownloadedSize);
            }
        }

        public int CachedSize
        {
            get
            {
                return downloadClients.Sum(client => client.CachedSize);
            }
        }
        
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
        
        List<HttpDownloadClient> downloadClients = null;

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

        public event EventHandler<DownloadEventArgs.DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

        public event EventHandler StatusChanged;

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
            
            downloadClients = new List<HttpDownloadClient>();

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
        
        public void Download()
        {
            if (Status != DownloadStatus.Initialized)
                throw new ApplicationException("Only Initialized download client can be started.");

            EnsurePropertyValid();

            Status = DownloadStatus.Downloading;

            if (!HasChecked)
            {
                string filename = null;
                CheckUrlAndFile(out filename);
            }

            HttpDownloadClient client = new HttpDownloadClient(Url.AbsoluteUri, 0, long.MaxValue,
                BufferSize, BufferCountPerNotification * BufferSize, BufferCountPerNotification)
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

            downloadClients.Add(client);
            client.Download();
        }
        
        public void BeginDownload()
        {
            if (Status != DownloadStatus.Initialized)
                throw new ApplicationException("Only Initialized download client can be started.");

            Status = DownloadStatus.Waiting;

            ThreadPool.QueueUserWorkItem(DownloadInternal);
        }

        void DownloadInternal(object obj)
        {

            if (Status != DownloadStatus.Waiting)
                return;

            try
            {
                EnsurePropertyValid();

                Status = DownloadStatus.Downloading;

                if (!HasChecked)
                {
                    string filename = null;
                    CheckUrlAndFile(out filename);
                }

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

                    downloadClients.Add(client);
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

                        downloadClients.Add(client);
                    }
                }
                
                lastStartTime = DateTime.Now;
                
                foreach (var client in downloadClients)
                {
                    if (Proxy != null)
                        client.Proxy = Proxy;
                    
                    client.DownloadProgressChanged += client_DownloadProgressChanged;
                    client.StatusChanged += client_StatusChanged;
                    client.DownloadCompleted += client_DownloadCompleted;
                    
                    client.BeginDownload();
                }
            }
            catch (Exception ex)
            {
                Cancel();
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
            }
        }
        
        public void Pause()
        {
            if (Status != DownloadStatus.Downloading)
                throw new ApplicationException("Only downloading downloader can be paused.");

            Status = DownloadStatus.Pausing;
            
            foreach (var client in downloadClients)
                if (client.Status == DownloadStatus.Downloading)
                    client.Pause();
        }
        
        public void Resume()
        {
            if (this.Status != DownloadStatus.Paused)
                throw new ApplicationException("Only paused downloader can be resumed. ");

            lastStartTime = DateTime.Now;
            
            Status = DownloadStatus.Waiting;
            
            foreach (var client in downloadClients)
                if (client.Status != DownloadStatus.Completed)
                    client.Resume();
        }

        public void BeginResume()
        {
            if (Status != DownloadStatus.Paused)
                throw new ApplicationException("Only paused downloader can be resumed. ");
            
            lastStartTime = DateTime.Now;
            
            this.Status = DownloadStatus.Waiting;

            foreach (var client in downloadClients)
                if (client.Status != DownloadStatus.Completed)
                    client.BeginResume();

        }
        
        public void Cancel()
        {

            if (Status == DownloadStatus.Initialized || Status == DownloadStatus.Waiting || Status == DownloadStatus.Completed
                || Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled)
            {
                Status = DownloadStatus.Canceled;
            }
            else if (Status == DownloadStatus.Canceling || Status == DownloadStatus.Pausing || Status == DownloadStatus.Downloading)
                Status = DownloadStatus.Canceling;
            
            foreach (var client in downloadClients)
                client.Cancel();

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
                if (DownloadProgressChanged != null)
                {
                    int speed = 0;
                    DateTime current = DateTime.Now;
                    TimeSpan interval = current - lastNotificationTime;

                    if (interval.TotalSeconds < 60)
                        speed = (int)Math.Floor((DownloadedSize + CachedSize - lastNotificationDownloadedSize) / interval.TotalSeconds);

                    lastNotificationTime = current;
                    lastNotificationDownloadedSize = DownloadedSize + CachedSize;

                    var downloadProgressChangedEventArgs = new DownloadEventArgs.DownloadProgressChangedEventArgs(DownloadedSize, TotalSize, speed);
                    OnDownloadProgressChanged(downloadProgressChangedEventArgs);
                }

            }
        }
        
        void client_DownloadCompleted(object sender, DownloadCompletedEventArgs e)
        {
            if (e.Error != null && Status != DownloadStatus.Canceling && Status != DownloadStatus.Canceled)
            {
                Cancel();
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, e.Error));
            }
        }
        
        protected virtual void OnDownloadProgressChanged(DownloadEventArgs.DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
        
        protected virtual void OnStatusChanged(EventArgs e)
        {

            switch (Status)
            {
                case DownloadStatus.Waiting:
                case DownloadStatus.Downloading:
                case DownloadStatus.Paused:
                case DownloadStatus.Canceled:
                case DownloadStatus.Completed:
                    StatusChanged?.Invoke(this, e);
                    break;
                default:
                    break;
            }

            if (Status == DownloadStatus.Paused || Status == DownloadStatus.Canceled || Status == DownloadStatus.Completed)
                usedTime += DateTime.Now - lastStartTime;
            
            if (Status == DownloadStatus.Canceled)
            {
                Exception ex = new Exception("Downloading is canceled by user's request. ");
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
            }

            if (Status == DownloadStatus.Completed)
                OnDownloadCompleted(new DownloadCompletedEventArgs(new FileInfo(DownloadPath), DownloadedSize, TotalSize, TotalUsedTime, null));
        }
        
        protected virtual void OnDownloadCompleted(DownloadCompletedEventArgs e)
        {
            DownloadCompleted?.Invoke(this, e);
        }

        #endregion
    }
}

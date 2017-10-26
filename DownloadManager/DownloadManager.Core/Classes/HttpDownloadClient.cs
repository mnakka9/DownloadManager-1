using System;
using System.IO;
using System.Net;
using System.Threading;
using DownloadManager.Core.Enums;
using DownloadManager.Core.Interfaces;
using DownloadManager.Core.DownloadEventArgs;

namespace DownloadManager.Core.Classes
{
    public class HttpDownloadClient: IDownloader, IObserver
    {
        #region Fields and Properties

        static object fileLocker = new object();

        object statusLocker = new object();
        
        public Uri Url { get; private set; }
        
        public string DownloadPath { get; set; }
        
        public long TotalSize { get; set; }

        public ICredentials Credentials { get; set; }

        public IWebProxy Proxy { get; set; }
        
        public bool IsRangeSupported { get; set; }
        
        public long StartPoint { get; set; }

        public long EndPoint { get; set; }
        
        public long DownloadedSize { get; private set; }

        public int CachedSize { get; private set; }

        public bool HasChecked { get; set; }

        DownloadStatus status;
        public DownloadStatus Status
        {
            get
            {
                return status;
            }
            private set
            {
                lock (statusLocker)
                {
                    if (status != value)
                    {
                        status = value;
                        OnStatusChanged(EventArgs.Empty);
                    }
                }
            }
        }

        public Thread DownloadThread { get; set; }
        
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
        private Int64 lastNotificationDownloadedSize;
        
        public int BufferCountPerNotification { get; set; }
        
        public int BufferSize { get; set; }
        
        public int MaxCacheSize { get; set; }

        #endregion

        #region Events

        public event EventHandler<DownloadEventArgs.DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public event EventHandler<DownloadCompletedEventArgs> DownloadCompleted;

        public event EventHandler StatusChanged;

        #endregion

        #region Constructors

        public HttpDownloadClient(string url) : this(url, 0)
        {
        }
        
        public HttpDownloadClient(string url, long startPoint) : this(url, startPoint, int.MaxValue)
        {
        }

        public HttpDownloadClient(string url, long startPoint, long endPoint) : this(url, startPoint, endPoint, 1024, 1048576, 64)
        {
        }

        public HttpDownloadClient(string url, long startPoint, long endPoint, int bufferSize, int cacheSize, int bufferCountPerNotification)
        {

            StartPoint = startPoint;
            EndPoint = endPoint;
            BufferSize = bufferSize;
            MaxCacheSize = cacheSize;
            BufferCountPerNotification = bufferCountPerNotification;

            Url = new Uri(url, UriKind.Absolute);
            
            IsRangeSupported = true;
            
            status = DownloadStatus.Initialized;
        }

        #endregion

        #region Methods

        public void CheckUrl(out string fileName)
        {
            fileName = DownloaderHelper.CheckUrl(this);
        }
        
        void CheckFileOrCreateFile()
        {
            DownloaderHelper.CheckFileOrCreateFile(this, fileLocker);
        }

        void CheckUrlAndFile(out string fileName)
        {
            CheckUrl(out fileName);
            CheckFileOrCreateFile();

            HasChecked = true;
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
        }
        
        public void Download()
        {
            if (Status != DownloadStatus.Initialized)
                throw new ApplicationException("Only Initialized download client can be started.");

            Status = DownloadStatus.Waiting;

            DownloadThread = new Thread(new ThreadStart(DownloadInternal))
            {
                IsBackground = true
            };
            DownloadThread.Start();
        }
        
        public void Resume()
        {
            if (Status != DownloadStatus.Paused)
                throw new ApplicationException("Only paused client can be resumed.");

            Status = DownloadStatus.Waiting;

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

            HttpWebRequest webRequest = null;
            HttpWebResponse webResponse = null;
            Stream responseStream = null;
            MemoryStream downloadCache = null;
            lastStartTime = DateTime.Now;

            try
            {

                if (!HasChecked)
                {
                    string filename = string.Empty;
                    CheckUrlAndFile(out filename);
                }

                EnsurePropertyValid();
                
                Status = DownloadStatus.Downloading;
                
                webRequest = DownloaderHelper.InitializeHttpWebRequest(this);
                
                if (EndPoint != int.MaxValue)
                    webRequest.AddRange(StartPoint + DownloadedSize, EndPoint);
                else
                    webRequest.AddRange(StartPoint + DownloadedSize);
                
                webResponse = (HttpWebResponse)webRequest.GetResponse();

                responseStream = webResponse.GetResponseStream();
                
                downloadCache = new MemoryStream(MaxCacheSize);

                byte[] downloadBuffer = new byte[BufferSize];

                int bytesSize = 0;
                CachedSize = 0;
                int receivedBufferCount = 0;
                
                while (true)
                {
                    bytesSize = responseStream.Read(downloadBuffer, 0, downloadBuffer.Length);
                    
                    if (Status != DownloadStatus.Downloading || bytesSize == 0 || MaxCacheSize < CachedSize + bytesSize)
                    {
                        try
                        {
                            WriteCacheToFile(downloadCache, CachedSize);

                            DownloadedSize += CachedSize;
 
                            if (Status != DownloadStatus.Downloading || bytesSize == 0)
                                break;
                            
                            downloadCache.Seek(0, SeekOrigin.Begin);
                            CachedSize = 0;
                        }
                        catch (Exception ex)
                        {
                            OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
                            return;
                        }

                    }
                    
                    downloadCache.Write(downloadBuffer, 0, bytesSize);

                    CachedSize += bytesSize;

                    receivedBufferCount++;
                    
                    if (receivedBufferCount == BufferCountPerNotification)
                    {
                        InternalDownloadProgressChanged(CachedSize);
                        receivedBufferCount = 0;
                    }
                }
                
                usedTime = usedTime.Add(DateTime.Now - lastStartTime);
                
                if (Status == DownloadStatus.Pausing)
                    Status = DownloadStatus.Paused;
                else if (Status == DownloadStatus.Canceling)
                    Status = DownloadStatus.Canceled;
                else
                {
                    Status = DownloadStatus.Completed;
                    return;
                }
            }
            catch (Exception ex)
            {
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
                return;
            }
            finally
            {
                if (responseStream != null)
                    responseStream.Close();
                if (webResponse != null)
                    webResponse.Close();
                if (downloadCache != null)
                    downloadCache.Close();
            }
        }
                
        void WriteCacheToFile(MemoryStream downloadCache, int cachedSize)
        {
            lock (fileLocker)
            {
                using (FileStream fileStream = new FileStream(DownloadPath, FileMode.Open))
                {
                    byte[] cacheContent = new byte[cachedSize];
                    downloadCache.Seek(0, SeekOrigin.Begin);
                    downloadCache.Read(cacheContent, 0, cachedSize);
                    fileStream.Seek(DownloadedSize + StartPoint, SeekOrigin.Begin);
                    fileStream.Write(cacheContent, 0, cachedSize);
                }
            }
        }

        public void UpdateStatus(DownloadStatus status)
        {
            Status = status;
        }

        public void Run()
        {
            Download();
        }

        #endregion

        #region EventHandlers

        protected virtual void OnDownloadCompleted(DownloadCompletedEventArgs e)
        {
            if (e.Error != null && status != DownloadStatus.Canceled)
                Status = DownloadStatus.Completed;

            DownloadCompleted?.Invoke(this, e);
        }
        
        private void InternalDownloadProgressChanged(int cachedSize)
        {
            int speed = 0;
            DateTime current = DateTime.Now;
            TimeSpan interval = current - lastNotificationTime;

            if (interval.TotalSeconds < 60)
            {
                speed = (int)Math.Floor((DownloadedSize + cachedSize - lastNotificationDownloadedSize) / interval.TotalSeconds);
            }
            lastNotificationTime = current;
            lastNotificationDownloadedSize = DownloadedSize + cachedSize;

            OnDownloadProgressChanged(new DownloadEventArgs.DownloadProgressChangedEventArgs(DownloadedSize + cachedSize, TotalSize, speed));
            
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

            if (status == DownloadStatus.Canceled)
            {
                Exception ex = new Exception("Downloading is canceled by user's request. ");
                OnDownloadCompleted(new DownloadCompletedEventArgs(null, DownloadedSize, TotalSize, TotalUsedTime, ex));
            }

            if (Status == DownloadStatus.Completed)
                OnDownloadCompleted(new DownloadCompletedEventArgs(new FileInfo(DownloadPath), DownloadedSize, TotalSize, TotalUsedTime, null));
        }

        #endregion
    }
}

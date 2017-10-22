using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using DownloadManager.Core.Interfaces;

namespace DownloadManager.Core.Classes
{
    class DownloaderHelper
    {
        public static HttpWebRequest InitializeHttpWebRequest(IDownloader downloader)
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(downloader.Url);

            if (downloader.Credentials != null)
                webRequest.Credentials = downloader.Credentials;
            else
                webRequest.Credentials = CredentialCache.DefaultCredentials;

            if (downloader.Proxy != null)
                webRequest.Proxy = downloader.Proxy;
            else
                webRequest.Proxy = WebRequest.DefaultWebProxy;

            return webRequest;
        }
        
        public static string CheckUrl(IDownloader downloader)
        {
            string fileName = string.Empty;
            
            var webRequest = InitializeHttpWebRequest(downloader);

            using (var response = webRequest.GetResponse())
            {
                foreach (var header in response.Headers.AllKeys)
                {
                    if (header.Equals("Accept-Ranges", StringComparison.OrdinalIgnoreCase))
                        downloader.IsRangeSupported = true;

                    if (header.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
                    {
                        string contentDisposition = response.Headers[header];

                        string pattern = ".[^;]*;\\s+filename=\"(?<file>.*)\"";
                        Regex r = new Regex(pattern);
                        Match m = r.Match(contentDisposition);
                        if (m.Success)
                            fileName = m.Groups["file"].Value;
                    }
                }

                downloader.TotalSize = response.ContentLength;

                if (downloader.TotalSize <= 0)
                    throw new ApplicationException("The file to download does not exist!");

                if (!downloader.IsRangeSupported)
                {
                    downloader.StartPoint = 0;
                    downloader.EndPoint = int.MaxValue;
                }
            }

            if (downloader.IsRangeSupported && (downloader.StartPoint != 0 || downloader.EndPoint != long.MaxValue))
            {
                webRequest = InitializeHttpWebRequest(downloader);

                if (downloader.EndPoint != int.MaxValue)
                    webRequest.AddRange(downloader.StartPoint, downloader.EndPoint);
                else
                    webRequest.AddRange(downloader.StartPoint);
                using (var response = webRequest.GetResponse())
                {
                    downloader.TotalSize = response.ContentLength;
                }
            }

            return fileName;
        }
        
        public static void CheckFileOrCreateFile(IDownloader downloader, object fileLocker)
        {
            lock (fileLocker)
            {
                FileInfo fileToDownload = new FileInfo(downloader.DownloadPath);
                if (fileToDownload.Exists)
                {
                    if (fileToDownload.Length != downloader.TotalSize)
                        throw new ApplicationException("The download path already has a file which does not match the file to download. ");
                }
                else
                {
                    if (downloader.TotalSize == 0)
                        throw new ApplicationException("The file to download does not exist!");

                    using (FileStream fileStream = File.Create(downloader.DownloadPath))
                    {
                        long createdSize = 0;
                        byte[] buffer = new byte[4096];
                        while (createdSize < downloader.TotalSize)
                        {
                            int bufferSize = (downloader.TotalSize - createdSize) < 4096 ? (int)(downloader.TotalSize - createdSize) : 4096;
                            fileStream.Write(buffer, 0, bufferSize);
                            createdSize += bufferSize;
                        }
                    }
                }
            }
        }
    }
}

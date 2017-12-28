using System;
using System.Globalization;

namespace DownloadManager.Core.Classes
{
    static class StringsFormatter
    {
        internal static string FormatTimeSpanString(TimeSpan span)
        {
            string hours = ((int)span.TotalHours).ToString();
            string minutes = span.Minutes.ToString();
            string seconds = span.Seconds.ToString();
            if ((int)span.TotalHours < 10)
                hours = "0" + hours;
            if (span.Minutes < 10)
                minutes = "0" + minutes;
            if (span.Seconds < 10)
                seconds = "0" + seconds;

            return String.Format("{0}:{1}:{2}", hours, minutes, seconds);
        }

        internal static string FormatSizeString(long byteSize)
        {
            double kiloByteSize = byteSize / 1024D;
            double megaByteSize = kiloByteSize / 1024D;
            double gigaByteSize = megaByteSize / 1024D;

            if (byteSize < 1024)
                return String.Format(NumberFormatInfo.InvariantInfo, "{0} B", byteSize);
            else if (byteSize < 1048576)
                return String.Format(NumberFormatInfo.InvariantInfo, "{0:0.00} kB", kiloByteSize);
            else if (byteSize < 1073741824)
                return String.Format(NumberFormatInfo.InvariantInfo, "{0:0.00} MB", megaByteSize);
            else
                return String.Format(NumberFormatInfo.InvariantInfo, "{0:0.00} GB", gigaByteSize);
        }
        
        internal static string FormatSpeedString(int speed)
        {
            speed *= 8;
            float kbSpeed = speed / 1024F;
            float mbSpeed = kbSpeed / 1024F;

            if (speed <= 0)
                return String.Empty;
            else if (speed < 1024)
                return (speed).ToString() + " B/s";
            else if (speed < 1048576)
                return kbSpeed.ToString("#.00", NumberFormatInfo.InvariantInfo) + " kB/s";
            else
                return mbSpeed.ToString("#.00", NumberFormatInfo.InvariantInfo) + " MB/s";
        }
    }
}

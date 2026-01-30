using System;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;

namespace IISLogAnalyzer_WPF.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Method { get; set; } = string.Empty;
        public string UriStem { get; set; } = string.Empty;
        public string UriQuery { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ClientIp { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public int SubStatus { get; set; }
        public int TimeTaken { get; set; }
        public double TimeTakenSeconds => TimeTaken / 1000.0;
        public long BytesSent { get; set; }

        public bool IsError => StatusCode >= 400;

        public PackIconKind StatusIcon
        {
            get
            {
                if (StatusCode >= 200 && StatusCode < 300) return PackIconKind.Check;
                if (StatusCode >= 300 && StatusCode < 400) return PackIconKind.Autorenew;
                if (StatusCode >= 400) return PackIconKind.Alert;
                return PackIconKind.HelpCircle;
            }
        }

        public Brush StatusBrush
        {
            get
            {
                if (StatusCode >= 200 && StatusCode < 300) return Brushes.LimeGreen;
                if (StatusCode >= 300 && StatusCode < 400) return Brushes.DodgerBlue;
                if (StatusCode >= 400) return Brushes.Red;
                 return Brushes.Gray;
            }
        }
    }
}

using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace FlairX_Mod_Manager.Models
{
    public class GameBananaFileViewModel : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string FileName { get; set; } = "";
        public long FileSize { get; set; }
        public string? Description { get; set; }
        public string DownloadUrl { get; set; } = "";
        public int DownloadCount { get; set; }

        public string FileSizeFormatted => FormatFileSize(FileSize);
        public string DownloadCountFormatted => FormatCount(DownloadCount);
        public Visibility HasDescription => string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string FormatCount(int count)
        {
            if (count >= 1000000)
                return $"{count / 1000000.0:F1}M";
            if (count >= 1000)
                return $"{count / 1000.0:F1}K";
            return count.ToString();
        }

        #pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
        #pragma warning restore CS0067
    }
}

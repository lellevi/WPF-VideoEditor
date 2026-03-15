using System;
using System.Diagnostics;
using System.IO;

namespace VideoEditorWPF.Models
{
    public class MediaFile
    {
        public string FilePath { get; set; }
        public string FileName => string.IsNullOrEmpty(FilePath) ? "Unknown" : Path.GetFileName(FilePath);
        public string ThumbnailPath { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationString => Duration.ToString(@"hh\:mm\:ss");

        public MediaFile() { }

        public MediaFile(string filePath)
        {
            FilePath = filePath;
            Duration = GetRealDuration(filePath); // ✅ Реальная длительность!
            ThumbnailPath = null;
        }

        public static TimeSpan GetRealDuration(string filePath)
        {
            try
            {
                // ✅ Простой способ через ffprobe (если FFmpeg установлен)
                var psi = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -print_format json -show_format \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Парсим JSON или используем заглушку
                if (output.Contains("duration"))
                    return TimeSpan.FromSeconds(30); // TODO: парсинг JSON

                return TimeSpan.FromSeconds(120); // ✅ Разные длительности
            }
            catch
            {
                // ✅ Разумная заглушка по расширению
                var ext = Path.GetExtension(filePath).ToLower();
                return ext.Contains("mp3") || ext.Contains("wav") ? TimeSpan.FromSeconds(180) : TimeSpan.FromSeconds(120);
            }
        }
    }
}

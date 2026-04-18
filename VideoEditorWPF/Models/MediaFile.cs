using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Documents;
using IOPath = System.IO.Path;

namespace VideoEditorWPF.Models
{
    public class MediaFile
    {
        public string FilePath { get; set; }
        public string FileName => string.IsNullOrEmpty(FilePath) ? "Unknown" : IOPath.GetFileName(FilePath);
        public string ThumbnailPath { get; set; }
        public TimeSpan Duration { get; set; }
        public string DurationString => Duration.ToString(@"hh\:mm\:ss");

        public MediaFile() { }

        public MediaFile(string filePath)
        {
            FilePath = filePath;
            Duration = GetRealDuration(filePath);
            ThumbnailPath = null;
        }

        public static TimeSpan GetRealDuration(string filePath)
        {
            try
            {
                var ffprobePath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");
                if (File.Exists(ffprobePath))
                {
                    var result = RunFfprobeSync(ffprobePath, filePath);
                    if (result.success) return result.duration;
                }

                var ffmpegPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (File.Exists(ffmpegPath))
                {
                    var result = RunFfmpegSync(ffmpegPath, filePath);
                    if (result.success) return result.duration;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{ex.Message}");
            }

            var ext = IOPath.GetExtension(filePath).ToLower();
            return ext.Contains("mp3") || ext.Contains("wav")
                ? TimeSpan.FromSeconds(180)
                : TimeSpan.FromSeconds(120);
        }

        private static (bool success, TimeSpan duration) RunFfprobeSync(string ffprobePath, string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of csv=p=0 \"{filePath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, TimeSpan.Zero);

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (double.TryParse(output.Trim(), CultureInfo.InvariantCulture, out double durationSeconds) && durationSeconds > 0)
            {
                return (true, TimeSpan.FromSeconds(durationSeconds));
            }

            return (false, TimeSpan.Zero);
        }

        private static (bool success, TimeSpan duration) RunFfmpegSync(string ffmpegPath, string filePath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"\"{filePath}\" -v error -show_entries format=duration -of csv=p=0",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, TimeSpan.Zero);

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();


            string result = output.Trim();
            if (string.IsNullOrEmpty(result)) result = error.Trim();

            if (double.TryParse(result, CultureInfo.InvariantCulture, out double durationSeconds) && durationSeconds > 0)
            {
                return (true, TimeSpan.FromSeconds(durationSeconds));
            }

            return (false, TimeSpan.Zero);
        }

        public static async Task<double> GetDurationFFmpegAsync(string filePath)
        {
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var ffprobePath = Path.Combine(appDir, "ffmpeg", "ffprobe.exe");

                if (!File.Exists(ffprobePath))
                {
                    ffprobePath = Path.Combine(appDir, "ffmpeg", "ffmpeg.exe");
                }

                if (!File.Exists(ffprobePath))
                {
                    return 30.0;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = $"-v quiet -show_entries format=duration -of csv=\"p=0\" -i \"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return 30.0;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    throw new ArgumentException($"ffprobe error: {stderr}");
                }

                var clean = output.Trim();
                if (string.IsNullOrWhiteSpace(clean))
                {
                    return 30.0;
                }

                if (double.TryParse(
                        clean,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double duration) &&
                    duration > 0.0 &&
                    duration <= 86400.0)
                {
                    return duration;
                }

                return 30.0;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"GetDurationFFmpegAsync: error: {ex.Message}");
                return 30.0;
            }
        }

    }
}
// Хранит путь, имя, длительность, превью. Автоматически получает реальную длительность через ffprobe/ffmpeg.
// Fallback: 3мин для аудио, 2мин для видео при ошибке анализа.
// Запускает внешние процессы синхронно для точного определения duration через CLI утилиты FFmpeg.

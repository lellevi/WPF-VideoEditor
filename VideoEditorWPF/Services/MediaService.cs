using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoEditorWPF.Models;
using IOPath = System.IO.Path;

namespace VideoEditorWPF.Services
{
    public interface IMediaService
    {
        Task<MediaFile> LoadMediaFileAsync(string filePath);
        Task<List<MediaFile>> LoadMultipleMediaFilesAsync(string[] filePaths);
    }

    public class MediaService : IMediaService
    {
        public async Task<MediaFile> LoadMediaFileAsync(string filePath)
        {
            var mediaFile = new MediaFile
            {
                FilePath = filePath,
                ThumbnailPath = await GenerateThumbnailAsync(filePath),
                Duration = await GetRealDurationAsync(filePath)
            };
            return mediaFile;
        }

        private async Task<TimeSpan> GetRealDurationAsync(string filePath)
        {
            try
            {
                var ffprobePath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffprobe.exe");
                if (File.Exists(ffprobePath))
                {
                    var result = await RunFfprobe(ffprobePath, filePath);
                    if (result.success) return result.duration;
                }

                var ffmpegPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                if (File.Exists(ffmpegPath))
                {
                    var result = await RunFfmpeg(ffmpegPath, filePath);
                    if (result.success) return result.duration;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{ex.Message}");
            }

            return TimeSpan.Zero;
        }

        private async Task<(bool success, TimeSpan duration)> RunFfprobe(string ffprobePath, string filePath)
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

            string output = await outputTask;
            string error = await errorTask;

            if (double.TryParse(output.Trim(), System.Globalization.CultureInfo.InvariantCulture, out double durationSeconds) && durationSeconds > 0)
            {
                return (true, TimeSpan.FromSeconds(durationSeconds));
            }

            return (false, TimeSpan.Zero);
        }

        private async Task<(bool success, TimeSpan duration)> RunFfmpeg(string ffmpegPath, string filePath)
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

            string output = await outputTask;
            string error = await errorTask;

            string result = output.Trim();
            if (string.IsNullOrEmpty(result)) result = error.Trim();

            if (double.TryParse(result, System.Globalization.CultureInfo.InvariantCulture, out double durationSeconds) && durationSeconds > 0)
            {
                return (true, TimeSpan.FromSeconds(durationSeconds));
            }

            return (false, TimeSpan.Zero);
        }

        public async Task<List<MediaFile>> LoadMultipleMediaFilesAsync(string[] filePaths)
        {
            var tasks = filePaths.Select(LoadMediaFileAsync);
            return (await Task.WhenAll(tasks)).ToList();
        }

        private async Task<string> GenerateThumbnailAsync(string filePath)
        {
            try
            {
                var ffmpegPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                var thumbPath = IOPath.Combine(IOPath.GetTempPath(), $"thumb_{Guid.NewGuid():N}.png");

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-ss 1 -i \"{filePath}\" -frames:v 1 -vf scale=80:60 \"{thumbPath}\" -y",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    await Task.Run(() => process.WaitForExit(2000));
                }

                if (File.Exists(thumbPath))
                    return thumbPath;
            }
            catch { }

            return CreateFallbackThumbnail();
        }

        private string CreateFallbackThumbnail()
        {
            string fallbackPath = IOPath.Combine(IOPath.GetTempPath(), $"thumb_fallback_{Guid.NewGuid():N}.png");
            var bitmap = new RenderTargetBitmap(80, 60, 96, 96, PixelFormats.Pbgra32);
            var rect = new Rectangle { Fill = new SolidColorBrush(Colors.DodgerBlue), RadiusX = 5, RadiusY = 5 };
            bitmap.Render(rect);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(fallbackPath))
                encoder.Save(stream);
            return fallbackPath;
        }
    }
}
// Сервис загрузки медиафайлов с асинхронным анализом.
// Генерирует thumbnails (80x60, 1с FFmpeg) + реальную длительность (ffprobe/ffmpeg async).
// Batch-загрузка нескольких файлов. Fallback: синий прямоугольник без FFmpeg.
// Сохраняет thumbs во временную папку с уникальными GUID именами.

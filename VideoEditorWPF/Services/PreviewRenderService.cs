using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoEditorWPF.Models;
using Drawing = System.Drawing;

namespace VideoEditorWPF.Services
{
    /// <summary>
    /// Интерфейс для рендеринга видео превью
    /// </summary>
    public interface IPreviewRenderService
    {
        WriteableBitmap InitializePreview(int width = 640, int height = 360);
        Task UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks);
        void SetPreviewFPS(int fps);
        void PreloadVideoFrames(string filePath);
    }

    /// <summary>
    /// Кэш кадров для видео
    /// </summary>
    public class FrameCache
    {
        public Dictionary<int, byte[]> Frames { get; } = new Dictionary<int, byte[]>();
    }

    /// <summary>
    /// Декодер видео через FFmpeg
    /// </summary>
    public class VideoDecoder : IDisposable
    {
        private readonly string _filePath;
        public int Width { get; } = 640;
        public int Height { get; } = 360;

        public VideoDecoder(string filePath)
        {
            _filePath = filePath;
        }

        /// <summary>
        /// Асинхронно получает кадр из видео
        /// </summary>
        public async Task<byte[]> GetFrameAsync(string filePath, double timeInSeconds)
        {
            try
            {
                var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe");
                if (!File.Exists(ffmpegPath)) return null;

                var tempPng = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid():N}.png");

                var timeStr = timeInSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                var args = $"-ss {timeStr} -i \"{filePath}\" -frames:v 1 -vf \"scale={Width}:{Height}\" -pix_fmt bgra -y \"{tempPng}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && File.Exists(tempPng))
                {
                    var fi = new FileInfo(tempPng);
                    using var fs = new FileStream(tempPng, FileMode.Open, FileAccess.Read);
                    using var bitmap = new Drawing.Bitmap(fs);

                    var result = BitmapToBytes(bitmap);
                    return result;
                }
            }
            catch (ArgumentException ex) { throw new ArgumentException($"GetFrame error: {ex.Message}"); }
            return null;
        }


        /// <summary>
        /// Конвертирует Bitmap в byte[]
        /// </summary>
        private byte[] BitmapToBytes(Drawing.Bitmap bitmap)
        {
            var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, Drawing.Imaging.ImageLockMode.ReadOnly,
                Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                // Конвертируем ARGB -> BGRA
                //ConvertArgbToBgra(bytes);

                return bytes;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        /// <summary>
        /// Конвертирует ARGB в BGRA
        /// </summary>
        private void ConvertArgbToBgra(byte[] pixels)
        {
            for (int i = 0; i < pixels.Length; i += 4)
            {
                // Меняем R и B местами
                byte temp = pixels[i];      // B
                pixels[i] = pixels[i + 2];  // B = R
                pixels[i + 2] = temp;       // R = B
            }
        }

        public void Dispose()
        {
            // Очистка ресурсов
        }
    }

    /// <summary>
    /// Сервис для рендеринга превью видео
    /// </summary>
    public class PreviewRenderService : IPreviewRenderService
    {
        private int _previewWidth = 640;
        private int _previewHeight = 360;
        private int _previewFPS = 15;

        private readonly ConcurrentDictionary<string, FrameCache> _frameCaches = new();
        private readonly Dictionary<string, VideoDecoder> _videoDecoders = new();

        public WriteableBitmap InitializePreview(int width = 640, int height = 360)
        {
            _previewWidth = width;
            _previewHeight = height;

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            ClearBitmap(bitmap);

            return bitmap;
        }

        public void SetPreviewFPS(int fps)
        {
            _previewFPS = Math.Max(5, Math.Min(60, fps));
        }

        public async Task UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks)
        {
            if (bitmap == null)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var activeClipResult = FindActiveVideoClip(tracks, currentTime);
                    byte[] frameBytes = null;

                    if (activeClipResult.HasValue)
                    {
                        var (clip, timeInClip) = activeClipResult.Value;
                        frameBytes = await GetOrFetchFrame(clip.FilePath, timeInClip);
                    }

                    bitmap.Dispatcher.Invoke(() =>
                    {
                        bitmap.Lock();
                        try
                        {
                            if (frameBytes != null && frameBytes.Length == _previewWidth * _previewHeight * 4)
                            {
                                bitmap.WritePixels(
                                    new Int32Rect(0, 0, _previewWidth, _previewHeight),
                                    frameBytes,
                                    _previewWidth * 4,
                                    0);
                                bitmap.AddDirtyRect(new Int32Rect(0, 0, _previewWidth, _previewHeight));
                            }
                            else
                            {
                                DrawAnimatedPreview(bitmap, currentTime);
                            }
                        }
                        finally
                        {
                            bitmap.Unlock();
                        }
                    });
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"UpdatePreview error: {ex.Message}");
                }
            });
        }


        /// <summary>
        /// Получает кадр из кэша или декодирует новый
        /// </summary>

        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInClip)
        {
            var cached = GetCachedFrame(filePath, timeInClip);
            if (cached != null)
            {
                return cached;
            }

            var decoder = _videoDecoders.TryGetValue(filePath, out var d) ? d :
                          (_videoDecoders[filePath] = new VideoDecoder(filePath));

            var frameBytes = await decoder.GetFrameAsync(filePath, timeInClip);

            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(timeInClip * 30);  // 33мс точность
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }


        /// <summary>
        /// Находит активный видео клип на текущем времени
        /// </summary>
        // VideoEditorWPF/Services/PreviewRenderService.cs
        // Метод получения кадра с учетом обрезки

        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInClip, Clip clip = null)
        {
            double actualTimeInFile = timeInClip;

            // Если клип обрезан, корректируем время в исходном файле
            if (clip != null && clip.TrimStart > 0)
            {
                actualTimeInFile = clip.TrimStart + timeInClip;

                // Проверяем, не вышли ли за пределы обрезки
                if (actualTimeInFile >= clip.TrimEnd)
                {
                    return null; // За пределами обрезанной области
                }
            }

            var cached = GetCachedFrame(filePath, actualTimeInFile);
            if (cached != null) return cached;

            var decoder = _videoDecoders.TryGetValue(filePath, out var d)
                ? d : (_videoDecoders[filePath] = new VideoDecoder(filePath));

            var frameBytes = await decoder.GetFrameAsync(filePath, actualTimeInFile);

            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(actualTimeInFile * 30);
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }

        // Обновленный метод поиска активного клипа
        private (Clip clip, double timeInClip)? FindActiveVideoClip(IEnumerable<Track> tracks, TimeSpan currentTime)
        {
            double currentSeconds = currentTime.TotalSeconds;

            foreach (var track in tracks.OrderBy(t => t.TrackIndex))
            {
                if (track.IsLocked || track.IsMuted) continue;

                foreach (var clip in track.Clips)
                {
                    if (clip.IsVideoClip &&
                        currentSeconds >= clip.OffsetSeconds &&
                        currentSeconds < clip.OffsetSeconds + clip.DurationSeconds)
                    {
                        // Время внутри клипа на таймлайне
                        double timeInClip = currentSeconds - clip.OffsetSeconds;

                        // Возвращаем клип и время внутри обрезанного клипа
                        return (clip, timeInClip);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Получает кадр из кэша
        /// </summary>
        private byte[] GetCachedFrame(string filePath, double timeInClip)
        {
            if (!_frameCaches.TryGetValue(filePath, out var cache) || !cache.Frames.Any())
                return null;

            int targetKey = (int)(timeInClip * 30);

            for (int delta = -1; delta <= 1; delta++)
            {
                if (cache.Frames.TryGetValue(targetKey + delta, out var frame))
                    return frame;
            }

            return null;
        }

        /// <summary>
        /// Создает кэш для файла если его нет
        /// </summary>
        private void EnsureCache(string filePath)
        {
            if (!_frameCaches.ContainsKey(filePath))
                _frameCaches[filePath] = new FrameCache();
        }

        /// <summary>
        /// Предзагрузка кадров (пока отключена)
        /// </summary>
        public void PreloadVideoFrames(string filePath)
        {
            // FIXME: Временно отключено - конфликтует с кэшем
            // Можно реализовать в фоновом режиме
        }

        /// <summary>
        /// Рисует анимированное превью когда нет видео
        /// </summary>
        private void DrawAnimatedPreview(WriteableBitmap bitmap, TimeSpan currentTime)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            var frameData = new byte[width * height * 4];

            double progress = Math.Min(1.0, currentTime.TotalSeconds / 120.0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    double wave = Math.Sin(x * 0.02 + currentTime.TotalSeconds * 2) * 0.3 + 0.7;

                    byte r = (byte)(50 + progress * 150 + wave * 30);
                    byte g = (byte)(100 + wave * 50);
                    byte b = 200;

                    frameData[index] = b;
                    frameData[index + 1] = g;
                    frameData[index + 2] = r;
                    frameData[index + 3] = 255;
                }
            }

            bitmap.WritePixels(
                new Int32Rect(0, 0, width, height),
                frameData,
                width * 4,
                0);
        }

        /// <summary>
        /// Очищает bitmap черным цветом
        /// </summary>
        private void ClearBitmap(WriteableBitmap bitmap)
        {
            bitmap.Lock();
            try
            {
                var blackData = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                bitmap.WritePixels(
                    new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                    blackData,
                    bitmap.PixelWidth * 4,
                    0);
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }
}

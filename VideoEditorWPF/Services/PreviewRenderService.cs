using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoEditorWPF.Models;
using Xabe.FFmpeg;
using Drawing = System.Drawing;

namespace VideoEditorWPF.Services
{
    public interface IPreviewRenderService
    {
        WriteableBitmap InitializePreview(int width = 640, int height = 360);
        Task UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks);
        void SetPreviewFPS(int fps);
        void PreloadVideoFrames(string filePath);
    }

    public class FrameCache
    {
        public Dictionary<int, byte[]> Frames { get; } = new Dictionary<int, byte[]>();
    }

    public class VideoDecoder : IDisposable
    {
        private readonly string _filePath;
        private readonly int _width = 640, _height = 360;

        public VideoDecoder(string filePath) => _filePath = filePath;


        public async Task<byte[]> GetFrameAsync(string filePath, double timeInSeconds)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                Debug.WriteLine($"🎬 FFmpeg: {Path.GetFileName(filePath)} t={timeInSeconds:F3}s");

                var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                var tempPng = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid():N}.png");

                var args = $"-ss {timeInSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -i \"{filePath}\" -frames:v 1 -y \"{tempPng}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return null;

                await process.WaitForExitAsync();

                if (process.ExitCode == 0 && File.Exists(tempPng))
                {
                    // ✅ ЧИТАЕМ БЕЗ БЛОКИРОВКИ + НЕ УДАЛЯЕМ!
                    using var fs = new FileStream(tempPng, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var bitmap = new Drawing.Bitmap(fs);
                    Debug.WriteLine($"🖼️ Bitmap: {bitmap.Width}x{bitmap.Height}");

                    using var resized = new Drawing.Bitmap(bitmap, _width, _height);
                    var result = BitmapToBytes(resized);

                    // ✅ НЕ УДАЛЯЕМ! Temp файлы очистятся системой
                    Debug.WriteLine($"✅ ✅ ✅ КАДР: {result.Length} bytes");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"💥 {ex.Message}");
            }
            return null;
        }



        private byte[] BitmapToBytes(Drawing.Bitmap bitmap)
        {
            var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, Drawing.Imaging.ImageLockMode.ReadOnly,
                                     Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                byte[] bytes = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        public void Dispose() { }
    }


    public class PreviewRenderService : IPreviewRenderService
    {
        private int _previewWidth = 640, _previewHeight = 360;
        private readonly ConcurrentDictionary<string, FrameCache> _frameCaches = new();
        private readonly Dictionary<string, VideoDecoder> _videoDecoders = new();
        private CancellationTokenSource _preloadCts = new();

        public WriteableBitmap InitializePreview(int width = 640, int height = 360)
        {
            _previewWidth = width; _previewHeight = height;
            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
            ClearBitmap(bitmap);
            return bitmap;
        }

        public void SetPreviewFPS(int fps)
        {
            // Пустая реализация
        }

        // ✅ ИСПРАВЛЕННЫЙ метод - правильная работа с nullable tuple
        public async Task UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks)
        {
            if (bitmap == null) return;

            bitmap.Lock();
            try
            {
                var activeClipResult = FindActiveVideoClip(tracks, currentTime);
                if (activeClipResult.HasValue)
                {
                    var (clip, timeInClip) = activeClipResult.Value;
                    Debug.WriteLine($"🔍 Active: {clip.FilePath}, time: {timeInClip:F2}s");

                    // ✅ АСИНХРОННО получаем кадр
                    var frameBytes = await GetOrFetchFrame(clip.FilePath, timeInClip);

                    if (frameBytes != null && frameBytes.Length == bitmap.PixelWidth * bitmap.PixelHeight * 4)
                    {
                        bitmap.WritePixels(new Int32Rect(0, 0, _previewWidth, _previewHeight),
                                         frameBytes, _previewWidth * 4, 0);
                        Debug.WriteLine("✅ ✅ ✅ ВИДЕО КАДР ПОКАЗАН!");
                    }
                    else
                    {
                        DrawAnimatedPreview(bitmap, currentTime);
                        Debug.WriteLine("❌ Кадр не получен → заглушка");
                    }
                }
                else
                {
                    DrawAnimatedPreview(bitmap, currentTime);
                    Debug.WriteLine("❌ Нет активного клипа");
                }

                bitmap.AddDirtyRect(new Int32Rect(0, 0, _previewWidth, _previewHeight));
            }
            finally { bitmap.Unlock(); }
        }


        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInClip)
        {
            Debug.WriteLine($"🔍 GetOrFetchFrame: {Path.GetFileName(filePath)}, t={timeInClip:F3}s");

            // 1. Кэш
            var cached = GetCachedFrame(filePath, timeInClip);
            if (cached != null)
            {
                Debug.WriteLine("✅ Кэш HIT!");
                return cached;
            }

            // 2. Создаем decoder
            if (!_videoDecoders.TryGetValue(filePath, out var decoder))
            {
                Debug.WriteLine("🔧 Создаем новый VideoDecoder");
                decoder = new VideoDecoder(filePath);
                _videoDecoders[filePath] = decoder;
            }

            // 3. Вызываем GetFrameAsync
            Debug.WriteLine("🚀 Вызов GetFrameAsync...");
            var frameBytes = await decoder.GetFrameAsync(filePath, timeInClip);

            Debug.WriteLine($"📤 GetFrameAsync вернул: {(frameBytes != null ? $"{frameBytes.Length} bytes" : "NULL")}");

            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(timeInClip * 10) % 600;
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }


        private void EnsureCache(string filePath)
        {
            if (!_frameCaches.ContainsKey(filePath))
                _frameCaches[filePath] = new FrameCache();
        }

        public void PreloadVideoFrames(string filePath)
        {
            if (_frameCaches.ContainsKey(filePath)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    EnsureCache(filePath);
                    using var decoder = new VideoDecoder(filePath);

                    // ✅ Предзагружаем кадры по всему таймлайну (не только первые 5 сек)
                    var info = await FFmpeg.GetMediaInfo(filePath);
                    double duration = info.VideoStreams.FirstOrDefault()?.Duration.TotalSeconds ?? 60;
                    int frameCount = Math.Min(120, (int)(duration * 0.5)); // 0.5 fps для превью

                    for (int i = 0; i < frameCount; i++)
                    {
                        if (_preloadCts.Token.IsCancellationRequested) break;
                        double time = (i / (double)frameCount) * duration;
                        var frameBytes = await decoder.GetFrameAsync(filePath, time);

                        if (frameBytes?.Length == _previewWidth * _previewHeight * 4)
                        {
                            int key = (int)(time * 10) % 600; // Ключ по времени (0.1s точность)
                            _frameCaches[filePath].Frames[key] = frameBytes;
                        }
                    }
                    Debug.WriteLine($"✅ Preloaded {filePath}: {frameCount} frames over {duration}s");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Preload failed: {ex.Message}");
                }
            }, _preloadCts.Token);
        }



        // ✅ ИСПРАВЛЕН: возвращает nullable tuple
        private (Clip clip, double timeInClip)? FindActiveVideoClip(IEnumerable<Track> tracks, TimeSpan currentTime)
        {
            double currentSeconds = currentTime.TotalSeconds;
            foreach (var track in tracks.OrderBy(t => t.TrackIndex))
            {
                if (track.IsLocked || track.IsMuted) continue;

                foreach (var clip in track.Clips)
                {
                    if (clip.IsVideoClip &&
                        currentSeconds >= clip.StartTimeSeconds &&
                        currentSeconds < clip.StartTimeSeconds + clip.DurationSeconds)
                    {
                        return (clip, currentSeconds - clip.StartTimeSeconds);
                    }
                }
            }
            return null;
        }

        private byte[] GetCachedFrame(string filePath, double timeInClip)
        {
            if (!_frameCaches.TryGetValue(filePath, out var cache) || !cache.Frames.Any())
                return null;

            // ✅ Ищем ближайший кэшированный кадр (±0.5s)
            int targetKey = (int)(timeInClip * 10);
            for (int delta = 0; delta <= 5; delta++)
            {
                int[] keysToCheck = { targetKey + delta, targetKey - delta };
                foreach (int key in keysToCheck)
                {
                    if (key >= 0 && cache.Frames.TryGetValue(key % 600, out var frame))
                        return frame;
                }
            }
            return null;
        }



        private void DrawAnimatedPreview(WriteableBitmap bitmap, TimeSpan currentTime)
        {
            int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
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
                    byte b = (byte)(200);

                    frameData[index] = b; frameData[index + 1] = g;
                    frameData[index + 2] = r; frameData[index + 3] = 255;
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), frameData, width * 4, 0);
        }

        private void ClearBitmap(WriteableBitmap bitmap)
        {
            bitmap.Lock();
            try
            {
                var blackData = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight),
                                 blackData, bitmap.PixelWidth * 4, 0);
            }
            finally { bitmap.Unlock(); }
        }
    }
}

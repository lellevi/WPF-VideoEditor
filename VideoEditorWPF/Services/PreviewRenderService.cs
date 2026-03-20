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
        public void Dispose() { }

        public async Task<byte[]> GetFrameAsync(string filePath, double timeInSeconds)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
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
                    using var fs = new FileStream(tempPng, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var bitmap = new Drawing.Bitmap(fs);

                    using var resized = new Drawing.Bitmap(bitmap, _width, _height);
                    var result = BitmapToBytes(resized);

                    return result;
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"{ex.Message}");
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
    }

    public class PreviewRenderService : IPreviewRenderService
    {
        private int _previewWidth = 640, _previewHeight = 360;
        private int _previewFPS = 15;
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
            _previewFPS = Math.Max(5, Math.Min(60, fps));  // 5-60 FPS
        }

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

                    var frameBytes = await GetOrFetchFrame(clip.FilePath, timeInClip);

                    if (frameBytes != null && frameBytes.Length == bitmap.PixelWidth * bitmap.PixelHeight * 4)
                    {
                        bitmap.WritePixels(new Int32Rect(0, 0, _previewWidth, _previewHeight),
                                         frameBytes, _previewWidth * 4, 0);
                    }
                    else
                    {
                        DrawAnimatedPreview(bitmap, currentTime);
                    }
                }
                else
                {
                    DrawAnimatedPreview(bitmap, currentTime);
                }

                bitmap.AddDirtyRect(new Int32Rect(0, 0, _previewWidth, _previewHeight));
            }
            finally { bitmap.Unlock(); }
        }


        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInClip)
        {
            var cached = GetCachedFrame(filePath, timeInClip);
            if (cached != null) return cached;

            if (!_videoDecoders.TryGetValue(filePath, out var decoder))
            {
                decoder = new VideoDecoder(filePath);
                _videoDecoders[filePath] = decoder;
            }

            var frameBytes = await decoder.GetFrameAsync(filePath, timeInClip);

            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(timeInClip * 30);
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }


        private void EnsureCache(string filePath)
        {
            if (!_frameCaches.ContainsKey(filePath))
                _frameCaches[filePath] = new FrameCache();
        }

        //public void PreloadVideoFrames(string filePath)
        //{
        //    if (_frameCaches.ContainsKey(filePath)) return;

        //    _ = Task.Run(async () =>
        //    {
        //        try
        //        {
        //            EnsureCache(filePath);
        //            using var decoder = new VideoDecoder(filePath);

        //            var info = await FFmpeg.GetMediaInfo(filePath);
        //            double duration = info.VideoStreams.FirstOrDefault()?.Duration.TotalSeconds ?? 60;
        //            int frameCount = Math.Min(120, (int)(duration * 0.5));

        //            for (int i = 0; i < frameCount; i++)
        //            {
        //                if (_preloadCts.Token.IsCancellationRequested) break;
        //                double time = (i / (double)frameCount) * duration;
        //                var frameBytes = await decoder.GetFrameAsync(filePath, time);

        //                if (frameBytes?.Length == _previewWidth * _previewHeight * 4)
        //                {
        //                    int key = (int)(time * 10) % 600;
        //                    _frameCaches[filePath].Frames[key] = frameBytes;
        //                }
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            throw new ArgumentException($"{ex.Message}");
        //        }
        //    }, _preloadCts.Token);
        //}

        public void PreloadVideoFrames(string filePath)
        {
            //_ = Task.Run(async () =>
            //{
            //    EnsureCache(filePath);
            //    using var decoder = new VideoDecoder(filePath);

            //    var info = await FFmpeg.GetMediaInfo(filePath);
            //    double duration = info.VideoStreams.FirstOrDefault()?.Duration.TotalSeconds ?? 60;

            //    // ✅ 60 FPS предзагрузка (каждые 1/60 сек)
            //    int frameCount = Math.Min(3600, (int)(duration * 60)); // Макс 1 час
            //    int step = Math.Max(1, frameCount / 120); // 120 кадров максимум

            //    for (int i = 0; i < frameCount; i += step)
            //    {
            //        double time = (i / 60.0);  // 60 FPS
            //        if (time > duration) break;

            //        var frameBytes = await decoder.GetFrameAsync(filePath, time);
            //        if (frameBytes != null)
            //            _frameCaches[filePath].Frames[(int)(time * 60)] = frameBytes;
            //    }
            //});

            //ВРЕМЕННО ОТКЛЮЧЕНО - конфликтует с кэшем
            // FIXME
        }

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

            int targetKey = (int)(timeInClip * 30);
            for (int delta = -1; delta <= 1; delta++)
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
// Сервис предпросмотра видео в WriteableBitmap (640x60FPS).
// FFmpeg-декодирование кадров по времени + кэш FrameCache (30FPS key).
// Находит активный видео-клип на timeline, рендерит кадр.
// Fallback: анимированная волна. Preload временно отключен.

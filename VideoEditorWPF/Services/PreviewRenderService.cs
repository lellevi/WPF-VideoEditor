using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VideoEditorWPF.Interfaces;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
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
            if (bitmap == null) return;

            await Task.Run(async () =>
            {
                try
                {
                    var activeClipResult = FindActiveVideoClip(tracks, currentTime);
                    byte[] frameBytes = null;
                    if (activeClipResult.HasValue)
                    {
                        var (clip, timeInFile) = activeClipResult.Value;
                        frameBytes = await GetOrFetchFrame(clip.FilePath, timeInFile);
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
                catch (Exception ex)
                {
                    throw new ArgumentException($"UpdatePreview error: {ex.Message}");
                }
            });
        }
        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInSeconds)
        {
            double roundedTime = Math.Round(timeInSeconds, 3);
            var cached = GetCachedFrame(filePath, roundedTime);
            if (cached != null)
            {
                return cached;
            }

            var decoder = _videoDecoders.TryGetValue(filePath, out var d) ? d : (_videoDecoders[filePath] = new VideoDecoder(filePath));
            var frameBytes = await decoder.GetFrameAsync(filePath, roundedTime);
            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(roundedTime * 30);
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }
        private async Task<byte[]> GetOrFetchFrame(string filePath, double timeInClip, Clip clip = null)
        {
            double actualTimeInFile = timeInClip;

            if (clip != null && clip.TrimStart > 0)
            {
                actualTimeInFile = clip.TrimStart + timeInClip;
                if (actualTimeInFile >= clip.TrimEnd)
                {
                    return null;
                }
            }

            var cached = GetCachedFrame(filePath, actualTimeInFile);
            if (cached != null) return cached;
            var decoder = _videoDecoders.TryGetValue(filePath, out var d) ? d : (_videoDecoders[filePath] = new VideoDecoder(filePath));
            var frameBytes = await decoder.GetFrameAsync(filePath, actualTimeInFile);
            if (frameBytes != null)
            {
                EnsureCache(filePath);
                int cacheKey = (int)(actualTimeInFile * 30);
                _frameCaches[filePath].Frames[cacheKey] = frameBytes;
            }

            return frameBytes;
        }

        private (Clip clip, double timeInFile)? FindActiveVideoClip(IEnumerable<Track> tracks, TimeSpan currentTime)
        {
            double currentSeconds = currentTime.TotalSeconds;

            foreach (var track in tracks.OrderBy(t => t.TrackIndex))
            {
                if (track.IsLocked || track.IsMuted) continue;

                foreach (var clip in track.Clips)
                {
                    if (!clip.IsVideoClip) continue;

                    if (currentSeconds >= clip.OffsetSeconds && currentSeconds < clip.OffsetSeconds + clip.DurationSeconds)
                    {
                        double timeInClip = currentSeconds - clip.OffsetSeconds;
                        double timeInFile = clip.TrimStart + timeInClip;
                        if (timeInFile >= clip.TrimEnd)
                        {
                            continue;
                        }

                        return (clip, timeInFile);
                    }
                }
            }

            return null;
        }
        private byte[] GetCachedFrame(string filePath, double timeInSeconds)
        {
            if (!_frameCaches.TryGetValue(filePath, out var cache) || cache.Frames.Count == 0)
                return null;

            int targetKey = (int)(timeInSeconds * 30);
            for (int delta = -2; delta <= 2; delta++)
            {
                if (cache.Frames.TryGetValue(targetKey + delta, out var frame))
                    return frame;
            }

            return null;
        }
        private void EnsureCache(string filePath)
        {
            if (!_frameCaches.ContainsKey(filePath))
                _frameCaches[filePath] = new FrameCache();
        }

        public void PreloadVideoFrames(string filePath)
        {
        }

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

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), frameData, width * 4, 0);
        }
        private void ClearBitmap(WriteableBitmap bitmap)
        {
            bitmap.Lock();
            try
            {
                var blackData = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), blackData, bitmap.PixelWidth * 4, 0);
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }
}

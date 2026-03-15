using System;
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
using Xabe.FFmpeg;
using Drawing = System.Drawing;

namespace VideoEditorWPF.Services
{
    /// <summary>
    /// Интерфейс для рендеринга видео превью
    /// </summary>
    public interface IPreviewRenderService
    {
        /// <summary>
        /// Инициализирует WriteableBitmap для превью
        /// </summary>
        /// <param name="width">Ширина превью</param>
        /// <param name="height">Высота превью</param>
        /// <returns>WriteableBitmap для отображения</returns>
        WriteableBitmap InitializePreview(int width = 640, int height = 360);

        /// <summary>
        /// Обновляет превью для указанного времени и треков
        /// </summary>
        /// <param name="bitmap">WriteableBitmap для обновления</param>
        /// <param name="currentTime">Текущее время воспроизведения</param>
        /// <param name="tracks">Коллекция треков с клипами</param>
        void UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks);

        /// <summary>
        /// Устанавливает частоту кадров для превью
        /// </summary>
        /// <param name="fps">Частота кадров</param>
        void SetPreviewFPS(int fps);
    }

    /// <summary>
    /// Сервис для рендеринга видео превью
    /// Композитинг кадров из нескольких треков в один
    /// </summary>
    public class PreviewRenderService : IPreviewRenderService
    {
        private int _previewWidth;
        private int _previewHeight;
        private int _fps = 24;

        // Кэш декодеров для видеофайлов
        private Dictionary<string, VideoDecoder> _videoDecoders = new();

        /// <summary>
        /// Инициализирует WriteableBitmap для превью
        /// </summary>
        public WriteableBitmap InitializePreview(int width = 640, int height = 360)
        {
            _previewWidth = width;
            _previewHeight = height;

            // Создаем WriteableBitmap в UI потоке
            WriteableBitmap bitmap = null;

            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    bitmap = CreateWriteableBitmap(width, height);
                });
            }
            else
            {
                bitmap = CreateWriteableBitmap(width, height);
            }

            // Заполняем черным цветом
            ClearBitmap(bitmap);

            return bitmap;
        }

        /// <summary>
        /// Создает WriteableBitmap
        /// </summary>
        private WriteableBitmap CreateWriteableBitmap(int width, int height)
        {
            return new WriteableBitmap(
                width,
                height,
                96,  // DPI X
                96,  // DPI Y
                PixelFormats.Bgra32,  // Формат: Blue, Green, Red, Alpha
                null);
        }

        /// <summary>
        /// Устанавливает FPS
        /// </summary>
        public void SetPreviewFPS(int fps)
        {
            if (fps > 0 && fps <= 120)
                _fps = fps;
        }

        /// <summary>
        /// Обновляет превью для текущего времени
        /// </summary>
        public void UpdatePreview(WriteableBitmap bitmap, TimeSpan currentTime, IEnumerable<Track> tracks)
        {
            if (bitmap == null || tracks == null)
                return;

            try
            {
                // Находим активные клипы на текущем времени
                var activeClips = FindActiveClips(tracks, currentTime);

                if (activeClips.Count == 0)
                {
                    // Нет активных клипов - черный экран
                    ClearBitmap(bitmap);
                    return;
                }

                // Композитим кадры из всех активных клипов
                var compositeFrame = CompositeFrame(activeClips, currentTime);

                if (compositeFrame != null)
                {
                    // Копируем в WriteableBitmap
                    CopyToWriteableBitmap(bitmap, compositeFrame);
                }
                else
                {
                    // Не удалось создать кадр - показываем заглушку
                    ShowPlaceholder(bitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка UpdatePreview: {ex.Message}");
                ShowErrorPlaceholder(bitmap);
            }
        }

        /// <summary>
        /// Находит все активные клипы на указанном времени
        /// </summary>
        private List<(Clip clip, double timeInClip)> FindActiveClips(IEnumerable<Track> tracks, TimeSpan currentTime)
        {
            var activeClips = new List<(Clip clip, double timeInClip)>();
            double currentSeconds = currentTime.TotalSeconds;

            foreach (var track in tracks.OrderBy(t => t.TrackIndex))
            {
                // Пропускаем заблокированные и отключенные треки
                if (track.IsLocked || track.IsMuted)
                    continue;

                foreach (var clip in track.Clips)
                {
                    double clipStart = clip.OffsetSeconds;
                    double clipEnd = clip.OffsetSeconds + clip.DurationSeconds;

                    // Проверяем, активен ли клип в текущий момент
                    if (currentSeconds >= clipStart && currentSeconds < clipEnd)
                    {
                        double timeInClip = currentSeconds - clipStart;
                        activeClips.Add((clip, timeInClip));
                    }
                }
            }

            return activeClips;
        }

        /// <summary>
        /// Композитит кадры из нескольких клипов
        /// Использует System.Drawing для композитинга
        /// </summary>
        private byte[] CompositeFrame(List<(Clip clip, double timeInClip)> activeClips, TimeSpan currentTime)
        {
            if (activeClips.Count == 0)
                return null;

            try
            {
                // Создаем результирующий Bitmap
                using (var compositeBitmap = new Drawing.Bitmap(_previewWidth, _previewHeight))
                using (var graphics = Drawing.Graphics.FromImage(compositeBitmap))
                {
                    // Заливаем черным фоном
                    graphics.Clear(Drawing.Color.Black);

                    // Рендерим каждый клип поверх предыдущих
                    foreach (var (clip, timeInClip) in activeClips)
                    {
                        RenderClipToGraphics(graphics, clip, timeInClip);
                    }

                    // Конвертируем в byte[] в формате BGRA
                    return BitmapToByteArray(compositeBitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка композитинга: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Рендерит один клип на Graphics
        /// </summary>
        private void RenderClipToGraphics(Drawing.Graphics graphics, Clip clip, double timeInClip)
        {
            try
            {
                // Для видео клипов - декодируем кадр
                if (clip.IsVideoClip)
                {
                    var frame = GetVideoFrame(clip.FilePath, timeInClip);
                    if (frame != null)
                    {
                        graphics.DrawImage(frame, 0, 0, _previewWidth, _previewHeight);
                    }
                }
                else
                {
                    // Для аудио клипов - показываем индикатор
                    RenderAudioIndicator(graphics, clip);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка рендеринга клипа {clip.FilePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает кадр из видео на указанном времени
        /// </summary>
        private Drawing.Bitmap GetVideoFrame(string filePath, double timeInSeconds)
        {
            try
            {
                // Проверяем существование файла
                if (!File.Exists(filePath))
                    return null;

                // Получаем или создаем декодер
                if (!_videoDecoders.TryGetValue(filePath, out var decoder))
                {
                    decoder = new VideoDecoder(filePath);
                    _videoDecoders[filePath] = decoder;
                }

                // Получаем кадр
                var frameData = decoder.GetFrameAtTime(timeInSeconds);
                if (frameData != null && frameData.Length > 0)
                {
                    // Конвертируем byte[] в Bitmap
                    return ByteArrayToBitmap(frameData, decoder.Width, decoder.Height);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка получения кадра: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Конвертирует System.Drawing.Bitmap в byte[] (BGRA формат)
        /// </summary>
        private byte[] BitmapToByteArray(Drawing.Bitmap bitmap)
        {
            var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bitmapData = bitmap.LockBits(rect,
                Drawing.Imaging.ImageLockMode.ReadOnly,
                Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                int bytes = Math.Abs(bitmapData.Stride) * bitmap.Height;
                byte[] pixelData = new byte[bytes];
                Marshal.Copy(bitmapData.Scan0, pixelData, 0, bytes);

                // Конвертируем ARGB -> BGRA (меняем местами R и B)
                ConvertArgbToBgra(pixelData);

                return pixelData;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        /// <summary>
        /// Конвертирует byte[] (BGRA) в System.Drawing.Bitmap
        /// </summary>
        private Drawing.Bitmap ByteArrayToBitmap(byte[] data, int width, int height)
        {
            var bitmap = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppArgb);
            var rect = new Drawing.Rectangle(0, 0, width, height);
            var bitmapData = bitmap.LockBits(rect,
                Drawing.Imaging.ImageLockMode.WriteOnly,
                Drawing.Imaging.PixelFormat.Format32bppArgb);

            try
            {
                Marshal.Copy(data, 0, bitmapData.Scan0, data.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
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

        /// <summary>
        /// Копирует byte[] в WriteableBitmap
        /// </summary>
        private void CopyToWriteableBitmap(WriteableBitmap bitmap, byte[] frameData)
        {
            if (frameData == null || frameData.Length != _previewWidth * _previewHeight * 4)
                return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                bitmap.Lock();
                try
                {
                    Marshal.Copy(frameData, 0, bitmap.BackBuffer, frameData.Length);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, _previewWidth, _previewHeight));
                }
                finally
                {
                    bitmap.Unlock();
                }
            });
        }

        /// <summary>
        /// Заполняет bitmap черным цветом
        /// </summary>
        private void ClearBitmap(WriteableBitmap bitmap)
        {
            var blackFrame = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            CopyToWriteableBitmap(bitmap, blackFrame);
        }

        /// <summary>
        /// Показывает заглушку с градиентом
        /// </summary>
        private void ShowPlaceholder(WriteableBitmap bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            var placeholderData = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte grayValue = (byte)(30 + (y * 20 / height));

                    placeholderData[index] = grayValue;     // B
                    placeholderData[index + 1] = grayValue; // G
                    placeholderData[index + 2] = grayValue; // R
                    placeholderData[index + 3] = 255;       // A
                }
            }

            CopyToWriteableBitmap(bitmap, placeholderData);
        }

        /// <summary>
        /// Показывает заглушку с ошибкой (красный экран)
        /// </summary>
        private void ShowErrorPlaceholder(WriteableBitmap bitmap)
        {
            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            var errorData = new byte[width * height * 4];

            for (int i = 0; i < errorData.Length; i += 4)
            {
                errorData[i] = 0;       // B
                errorData[i + 1] = 0;   // G
                errorData[i + 2] = 128; // R (темно-красный)
                errorData[i + 3] = 255; // A
            }

            CopyToWriteableBitmap(bitmap, errorData);
        }

        /// <summary>
        /// Рендерит индикатор аудио клипа
        /// </summary>
        private void RenderAudioIndicator(Drawing.Graphics graphics, Clip clip)
        {
            // Простой индикатор - оранжевая полоска снизу
            var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(128, 255, 140, 0));
            var rect = new Drawing.Rectangle(0, _previewHeight - 30, _previewWidth, 30);
            graphics.FillRectangle(brush, rect);

            // Текст с названием файла
            var fileName = Path.GetFileName(clip.FilePath);
            var font = new Drawing.Font("Arial", 10);
            var textBrush = new Drawing.SolidBrush(Drawing.Color.White);
            graphics.DrawString($"🔊 {fileName}", font, textBrush, 10, _previewHeight - 25);
        }
        /// <summary>
        /// Декодер видео с использованием Xabe.FFmpeg
        /// </summary>
        private class VideoDecoder : IDisposable
        {
            private readonly string _filePath;
            private IMediaInfo _mediaInfo;

            public int Width { get; private set; } = 640;
            public int Height { get; private set; } = 360;

            public VideoDecoder(string filePath)
            {
                _filePath = filePath;
                InitializeAsync().Wait();
            }

            private async Task InitializeAsync()
            {
                try
                {
                    _mediaInfo = await Xabe.FFmpeg.FFmpeg.GetMediaInfo(_filePath);

                    var videoStream = _mediaInfo.VideoStreams.FirstOrDefault();
                    if (videoStream != null)
                    {
                        Width = videoStream.Width;
                        Height = videoStream.Height;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка инициализации VideoDecoder: {ex.Message}");
                }
            }

            public byte[] GetFrameAtTime(double timeInSeconds)
            {
                try
                {
                    // Создаем временный файл для кадра
                    var tempImagePath = Path.Combine(Path.GetTempPath(), $"frame_{Guid.NewGuid()}.png");

                    try
                    {
                        // Извлекаем кадр через Xabe.FFmpeg
                        var seekTime = TimeSpan.FromSeconds(timeInSeconds);
                        var task = Xabe.FFmpeg.FFmpeg.Conversions.FromSnippet
                            .Snapshot(_filePath, tempImagePath, seekTime);
                        task.Wait();

                        if (!File.Exists(tempImagePath))
                            return CreateTestFrame();

                        // Загружаем PNG и конвертируем в byte[]
                        using (var bitmap = new Drawing.Bitmap(tempImagePath))
                        {
                            // Изменяем размер если нужно
                            using (var resized = new Drawing.Bitmap(bitmap, Width, Height))
                            {
                                return BitmapToByteArray(resized);
                            }
                        }
                    }
                    finally
                    {
                        // Удаляем временный файл
                        if (File.Exists(tempImagePath))
                        {
                            try { File.Delete(tempImagePath); }
                            catch { /* Игнорируем ошибки удаления */ }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка получения кадра: {ex.Message}");
                    return CreateTestFrame();
                }
            }

            private byte[] BitmapToByteArray(Drawing.Bitmap bitmap)
            {
                var rect = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var bitmapData = bitmap.LockBits(rect,
                    Drawing.Imaging.ImageLockMode.ReadOnly,
                    Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    int bytes = Math.Abs(bitmapData.Stride) * bitmap.Height;
                    byte[] pixelData = new byte[bytes];
                    Marshal.Copy(bitmapData.Scan0, pixelData, 0, bytes);
                    return pixelData;
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }

            private byte[] CreateTestFrame()
            {
                // Создаем простой тестовый кадр (синий градиент)
                var data = new byte[Width * Height * 4];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        int index = (y * Width + x) * 4;
                        data[index] = (byte)(100 + y * 155 / Height);  // B
                        data[index + 1] = 50;                          // G
                        data[index + 2] = 50;                          // R
                        data[index + 3] = 255;                         // A
                    }
                }
                return data;
            }

            public void Dispose()
            {
                _mediaInfo = null;
            }
        }
    }
}

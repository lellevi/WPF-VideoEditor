using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
    public interface IClipRenderService
    {
        void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale);
    }

    public class ClipRenderService : IClipRenderService
    {
        private const double TrackHeight = 70;

        public void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale)
        {
            canvas.Children.Clear();
            foreach (var clip in clips)
            {
                double startX = clip.GetOffsetPixels(timelineScale);
                double width = clip.GetWidth(timelineScale);

                if (double.IsNaN(startX) || double.IsInfinity(startX))
                {
                    Debug.WriteLine($"[RenderClip] Неверный startX для {clip.DisplayName} (OffsetSeconds={clip.OffsetSeconds}, timelineScale={timelineScale})");
                    startX = 0; // подстраховка
                }

                if (double.IsNaN(width) || double.IsInfinity(width))
                {
                    Debug.WriteLine($"[RenderClip] Неверный width для {clip.DisplayName}: {width}");
                    width = 50; // подстраховка
                }
                RenderClip(canvas, clip, trackTop, timelineScale);
            }
        }

        private void RenderClip(Canvas canvas, Clip clip, double top, double timelineScale)
        {
            Debug.WriteLine($"ClipRenderService.RenderClip: {clip.DisplayName} (OffsetSeconds={clip.OffsetSeconds}, DurationSeconds={clip.DurationSeconds}, timelineScale={timelineScale:F3})");

            double startX = clip.GetOffsetPixels(timelineScale);
            Debug.WriteLine($"ClipRenderService.RenderClip: startX = {startX:F3}");
            double width = clip.GetWidth(timelineScale);

            if (double.IsNaN(startX) || double.IsInfinity(startX))
            {
                Debug.WriteLine($"[RenderClip] Неверный startX (OffsetSeconds={clip.OffsetSeconds})");
                startX = 0; // подстраховка
            }

            if (double.IsNaN(width) || double.IsInfinity(width))
            {
                Debug.WriteLine($"[RenderClip] Неверный width (DurationSeconds={clip.DurationSeconds})");
                width = 50; // подстраховка
            }
            Debug.WriteLine($"ClipRenderService.RenderClip: startX = {startX:F3} (OffsetSeconds={clip.OffsetSeconds:F3}, timelineScale={timelineScale:F3})");
            Brush color = clip.IsVideoClip ? Brushes.DodgerBlue : Brushes.Orange;

            var rect = new Rectangle
            {
                Width = clip.GetWidth(timelineScale),
                Height = TrackHeight,
                Fill = color,
                RadiusX = 5,
                RadiusY = 5,
                Tag = clip
            };
            Debug.WriteLine($"[RenderClip] Rendering {clip.FilePath} at {Canvas.GetLeft(rect)}, Width={rect.Width}");

            Canvas.SetLeft(rect, clip.GetOffsetPixels(timelineScale));
            Canvas.SetTop(rect, top);
            Canvas.SetZIndex(rect, 10);
            canvas.Children.Add(rect);

            var fileName = clip.FilePath.Split("\\")[^1];

            string displayName = TruncateFileName(fileName, 20);
            var label = new TextBlock
            {
                Text = displayName,
                Foreground = Brushes.White,
                FontSize = 10,
                Tag = clip
            };

            Canvas.SetLeft(label, clip.GetOffsetPixels(timelineScale) + 5);
            Canvas.SetTop(label, top + (TrackHeight / 2) - 5); // Center vertically
            Canvas.SetZIndex(label, 11);
            canvas.Children.Add(label);
        }

        private string TruncateFileName(string fileName, int maxLength)
        {
            if (fileName.Length <= maxLength)
                return fileName;

            int prefixLength = maxLength / 2 - 2;
            int suffixLength = maxLength / 2 - 2;

            return string.Concat(fileName.AsSpan(0, prefixLength), "...", fileName.AsSpan(fileName.Length - suffixLength));
        }

        // В ClipRenderService.cs
        private void DrawTrimIndicators(Canvas canvas, Clip clip, double x, double y, double width, double height)
        {
            if (clip.TrimStart > 0)
            {
                // Затемненная область в начале клипа (обрезанная часть)
                double trimmedStartWidth = (clip.TrimStart / clip.SourceDuration) * width;
                var trimmedStartOverlay = new Rectangle
                {
                    Width = trimmedStartWidth,
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                    Stroke = new SolidColorBrush(Colors.DarkRed),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(trimmedStartOverlay, x);
                Canvas.SetTop(trimmedStartOverlay, y);
                canvas.Children.Add(trimmedStartOverlay);
            }

            if (clip.TrimEnd < clip.SourceDuration)
            {
                // Затемненная область в конце клипа
                double trimmedEndWidth = ((clip.SourceDuration - clip.TrimEnd) / clip.SourceDuration) * width;
                double trimmedEndX = x + width - trimmedEndWidth;
                var trimmedEndOverlay = new Rectangle
                {
                    Width = trimmedEndWidth,
                    Height = height,
                    Fill = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                    Stroke = new SolidColorBrush(Colors.DarkRed),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(trimmedEndOverlay, trimmedEndX);
                Canvas.SetTop(trimmedEndOverlay, y);
                canvas.Children.Add(trimmedEndOverlay);
            }
        }
    }
}
// Сервис отрисовки клипов на Canvas timeline.
// Создает визуальные Rectangle (видео=синий, аудио=оранжевый) + TextBlock с усеченным именем файла.
// Устанавливает позицию/размер по StartX/Width, ZIndex для наложения.
// trackTop - отступ трека по Y. Обрезает длинные имена: "prefix...suffix".

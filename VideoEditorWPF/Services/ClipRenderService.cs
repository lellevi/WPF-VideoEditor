//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Windows.Controls;
//using System.Windows.Media;
//using System.Windows.Shapes;
//using VideoEditorWPF.Models;

//namespace VideoEditorWPF.Services
//{
//    public interface IClipRenderService
//    {
//        void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale);
//    }

//    public class ClipRenderService : IClipRenderService
//    {
//        private const double TrackHeight = 70;

//        public void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale)
//        {
//            canvas.Children.Clear();
//            foreach (var clip in clips)
//            {
//                double startX = clip.GetOffsetPixels(timelineScale);
//                double width = clip.GetWidth(timelineScale);

//                if (double.IsNaN(startX) || double.IsInfinity(startX))
//                {
//                    startX = 0;
//                }

//                if (double.IsNaN(width) || double.IsInfinity(width))
//                {
//                    width = 50;
//                }
//                RenderClip(canvas, clip, trackTop, timelineScale);
//            }
//        }

//        private void RenderClip(Canvas canvas, Clip clip, double top, double timelineScale)
//        {
//            double startX = clip.GetOffsetPixels(timelineScale);
//            double width = clip.GetWidth(timelineScale);

//            if (double.IsNaN(startX) || double.IsInfinity(startX))
//            {
//                startX = 0;
//            }

//            if (double.IsNaN(width) || double.IsInfinity(width))
//            {
//                width = 50;
//            }
//            Brush color = clip.IsVideoClip ? Brushes.DodgerBlue : Brushes.Orange;

//            var rect = new Rectangle
//            {
//                Width = clip.GetWidth(timelineScale),
//                Height = TrackHeight,
//                Fill = color,
//                RadiusX = 5,
//                RadiusY = 5,
//                Tag = clip
//            };

//            Canvas.SetLeft(rect, clip.GetOffsetPixels(timelineScale));
//            Canvas.SetTop(rect, top);
//            Canvas.SetZIndex(rect, 10);
//            canvas.Children.Add(rect);

//            var fileName = clip.FilePath.Split("\\")[^1];

//            string displayName = TruncateFileName(fileName, 20);
//            var label = new TextBlock
//            {
//                Text = displayName,
//                Foreground = Brushes.White,
//                FontSize = 10,
//                Tag = clip
//            };

//            Canvas.SetLeft(label, clip.GetOffsetPixels(timelineScale) + 5);
//            Canvas.SetTop(label, top + (TrackHeight / 2) - 5); // Center vertically
//            Canvas.SetZIndex(label, 11);
//            canvas.Children.Add(label);
//        }

//        private string TruncateFileName(string fileName, int maxLength)
//        {
//            if (fileName.Length <= maxLength)
//                return fileName;

//            int prefixLength = maxLength / 2 - 2;
//            int suffixLength = maxLength / 2 - 2;

//            return string.Concat(fileName.AsSpan(0, prefixLength), "...", fileName.AsSpan(fileName.Length - suffixLength));
//        }
//    }
//}

// В ClipRenderService.cs - убедитесь, что RenderClips правильно отображает клипы

using System;
using System.Collections.Generic;
using System.Windows;
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
            if (clips == null) return;

            foreach (var clip in clips)
            {
                RenderClip(canvas, clip, trackTop, timelineScale);
            }
        }

        private void RenderClip(Canvas canvas, Clip clip, double top, double timelineScale)
        {
            if (clip == null) return;

            double startX = clip.GetOffsetPixels(timelineScale);
            double width = clip.GetWidth(timelineScale);

            // Валидация значений
            if (double.IsNaN(startX) || double.IsInfinity(startX) || startX < 0)
                startX = 0;

            if (double.IsNaN(width) || double.IsInfinity(width) || width < 5)
                width = 50;

            // Выбираем цвет в зависимости от типа клипа
            Brush color = clip.IsVideoClip ? Brushes.DodgerBlue : Brushes.Orange;

            // Создаем прямоугольник клипа
            var rect = new Rectangle
            {
                Width = width,
                Height = TrackHeight - 4, // Немного меньше высоты трека для отступа
                Fill = color,
                RadiusX = 4,
                RadiusY = 4,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Opacity = 0.9,
                Tag = clip
            };

            Canvas.SetLeft(rect, startX);
            Canvas.SetTop(rect, top + 2); // Центрируем по вертикали
            Canvas.SetZIndex(rect, 10);
            canvas.Children.Add(rect);

            // Добавляем текст с именем файла
            string fileName = System.IO.Path.GetFileNameWithoutExtension(clip.FilePath);
            string displayName = fileName.Length > 25 ? fileName.Substring(0, 22) + "..." : fileName;

            // Добавляем номер инстанса если нужно
            if (clip.InstanceNumber > 1)
                displayName += $" ({clip.InstanceNumber})";

            var label = new TextBlock
            {
                Text = displayName,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Tag = clip,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = width - 10
            };

            Canvas.SetLeft(label, startX + 5);
            Canvas.SetTop(label, top + (TrackHeight / 2) - 8);
            Canvas.SetZIndex(label, 11);
            canvas.Children.Add(label);

            // Добавляем метку времени
            var timeLabel = new TextBlock
            {
                Text = $"{TimeSpan.FromSeconds(clip.DurationSeconds):mm\\:ss}",
                Foreground = Brushes.LightGray,
                FontSize = 8,
                Tag = clip
            };

            Canvas.SetLeft(timeLabel, startX + width - 35);
            Canvas.SetTop(timeLabel, top + TrackHeight - 18);
            Canvas.SetZIndex(timeLabel, 11);
            canvas.Children.Add(timeLabel);
        }
    }
}
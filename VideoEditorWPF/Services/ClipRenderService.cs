using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoEditorWPF.Interfaces;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
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

            if (double.IsNaN(startX) || double.IsInfinity(startX) || startX < 0)
                startX = 0;

            if (double.IsNaN(width) || double.IsInfinity(width) || width < 5)
                width = 50;

            Brush color = clip.IsVideoClip ? Brushes.DodgerBlue : Brushes.Orange;

            var rect = new Rectangle
            {
                Width = width,
                Height = TrackHeight - 4,
                Fill = color,
                RadiusX = 4,
                RadiusY = 4,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Opacity = 0.9,
                Tag = clip
            };

            Canvas.SetLeft(rect, startX);
            Canvas.SetTop(rect, top + 2);
            Canvas.SetZIndex(rect, 10);
            canvas.Children.Add(rect);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(clip.FilePath);
            string displayName = fileName.Length > 25 ? fileName.Substring(0, 22) + "..." : fileName;

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
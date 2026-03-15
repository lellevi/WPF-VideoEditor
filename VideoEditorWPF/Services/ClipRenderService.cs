using System;
using System.Collections.Generic;
using System.Linq;
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
        public void RenderClips(Canvas canvas, IEnumerable<Clip> clips, double trackTop, double timelineScale)
        {
            foreach (var clip in clips)
            {
                RenderClip(canvas, clip, trackTop, timelineScale);
            }
        }

        private void RenderClip(Canvas canvas, Clip clip, double top, double timelineScale)
        {
            Brush color = clip.IsVideoClip ? Brushes.DodgerBlue : Brushes.Orange;

            var rect = new Rectangle
            {
                Width = clip.GetWidth(timelineScale),
                Height = 40,
                Fill = color,
                RadiusX = 5,
                RadiusY = 5,
                Tag = clip
            };

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
            Canvas.SetTop(label, top + 15);
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
    }
}

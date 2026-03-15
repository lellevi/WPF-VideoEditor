using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
    public interface ITrackRenderService
    {
        void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale);
        void ClearTracks(Canvas canvas);
    }

    public class TrackRenderService : ITrackRenderService
    {
        private const double TRACK_HEADER_HEIGHT = 25;
        private const double TRACK_HEIGHT = 50;
        private const double TRACK_GAP = 5;

        private readonly IClipRenderService _clipRenderService;

        public TrackRenderService(IClipRenderService clipRenderService)
        {
            _clipRenderService = clipRenderService;
        }

        public void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale)
        {
            double currentY = TRACK_HEADER_HEIGHT + TRACK_GAP;

            foreach (var track in tracks)
            {
                DrawTrackBackground(canvas, currentY, canvasWidth);
                _clipRenderService.RenderClips(canvas, track.Clips, currentY, timelineScale);
                currentY += TRACK_HEIGHT + TRACK_GAP;
            }
        }

        public void ClearTracks(Canvas canvas)
        {
            var toRemove = canvas.Children.OfType<UIElement>()
                .Where(e =>
                {
                    double top = Canvas.GetTop(e);
                    return !double.IsNaN(top) && top >= TRACK_HEADER_HEIGHT;
                })
                .ToList();

            foreach (var elem in toRemove)
            {
                canvas.Children.Remove(elem);
            }
        }

        private void DrawTrackBackground(Canvas canvas, double y, double canvasWidth)
        {
            // Draw a subtle background for the track area
            var trackBg = new Rectangle
            {
                Width = canvasWidth,
                Height = TRACK_HEIGHT,
                Fill = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                Opacity = 0.3
            };
            Canvas.SetLeft(trackBg, 0);
            Canvas.SetTop(trackBg, y);
            Canvas.SetZIndex(trackBg, -10);
            canvas.Children.Add(trackBg);
        }
    }
}

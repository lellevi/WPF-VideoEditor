using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VideoEditorWPF.Interfaces;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Services
{
    public class TrackRenderService : ITrackRenderService
    {
        private const double TrackHeight = 70;
        private readonly IClipRenderService _clipRenderService;
        public event EventHandler<ClipPropertyChangedEventArgs> ClipPropertyChanged;

        public TrackRenderService(IClipRenderService clipRenderService)
        {
            _clipRenderService = clipRenderService;
        }

        public void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale)
        {
            ClearTracks(canvas);
            int trackIndex = 0;
            foreach (var track in tracks)
            {
                double trackY = trackIndex * TrackHeight;
                DrawTrackBackground(canvas, trackY, canvasWidth);
                _clipRenderService.RenderClips(canvas, track.Clips, trackY, timelineScale);
                trackIndex++;
            }
        }

        public void ForceRefresh(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale)
        {
            RenderTracks(canvas, tracks, canvasWidth, timelineScale);
        }
        public void ClearTracks(Canvas canvas)
        {
            var toRemove = canvas.Children.OfType<UIElement>()
                .Where(e =>
                {
                    int zIndex = Canvas.GetZIndex(e);
                    return zIndex < 1000;
                }).ToList();

            foreach (var elem in toRemove)
            {
                canvas.Children.Remove(elem);
            }
        }

        private void DrawTrackBackground(Canvas canvas, double y, double canvasWidth)
        {
            var trackBg = new Rectangle
            {
                Width = canvasWidth,
                Height = TrackHeight,
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
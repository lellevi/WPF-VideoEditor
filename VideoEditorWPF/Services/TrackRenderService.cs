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
        void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double viewportWidth);
        void ClearTracks(Canvas canvas);
    }

    public class TrackRenderService : ITrackRenderService
    {
        private const double TrackHeaderHeight = 25;
        private const double TrackHeight = 50;
        private const double TrackGap = 5;

        private readonly IClipRenderService _clipRenderService;

        public TrackRenderService(IClipRenderService clipRenderService)
        {
            _clipRenderService = clipRenderService;
        }

        public void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double viewportWidth)
        {
            double currentY = TrackHeaderHeight + TrackGap;

            foreach (var track in tracks)
            {
                DrawTrackHeader(canvas, track, currentY, viewportWidth);
                _clipRenderService.RenderClips(canvas, track.Clips, currentY);
                currentY += TrackHeight + TrackGap;
            }
        }

        public void ClearTracks(Canvas canvas)
        {
            var toRemove = canvas.Children.OfType<UIElement>()
                .Where(e =>
                {
                    double top = Canvas.GetTop(e);
                    return !double.IsNaN(top) && top >= TrackHeaderHeight;
                })
                .ToList();

            foreach (var elem in toRemove)
            {
                canvas.Children.Remove(elem);
            }
        }

        private void DrawTrackHeader(Canvas canvas, Track track, double y, double viewportWidth)
        {
            Color headerColor = track.Type == MediaType.Video
                ? Color.FromRgb(45, 45, 80)
                : Color.FromRgb(45, 65, 45);

            var headerBg = new Rectangle
            {
                Width = viewportWidth * 2,
                Height = TrackHeaderHeight,
                Fill = new SolidColorBrush(headerColor),
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            Canvas.SetLeft(headerBg, 0);
            Canvas.SetTop(headerBg, y);
            Canvas.SetZIndex(headerBg, 0);
            canvas.Children.Add(headerBg);

            string trackTypeIcon = track.Type == MediaType.Video ? "🎬" : "🎵";
            var label = new TextBlock
            {
                Text = $"{trackTypeIcon} {track.Name ?? (track.Type == MediaType.Video ? "Video" : "Audio")}",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, 10);
            Canvas.SetTop(label, y + 5);
            Canvas.SetZIndex(label, 1000);
            canvas.Children.Add(label);
        }
    }
}
// Сервис отрисовки треков timeline (Canvas).
// Рендерит заголовки треков (видео=темно-синий, аудио=темно-зеленый) + клипы.
// TrackHeight=50px, TrackHeaderHeight=25px, TrackGap=5px.
// Иконки: 🎬 видео, 🎵 аудио. Очищает область треков (>=25px).

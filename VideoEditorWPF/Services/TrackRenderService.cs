using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const double TrackHeight = 70; // Match MainWindow.xaml track height

        private readonly IClipRenderService _clipRenderService;

        public event EventHandler ClipPropertyChanged;

        private void OnClipPropertyChanged(Clip clip, string propertyName)
        {
            ClipPropertyChanged?.Invoke(this, new ClipPropertyChangedEventArgs(clip, propertyName));
        }

        private void SubscribeToClips(IEnumerable<Clip> clips, string propertyName = null)
        {
            foreach (var clip in clips)
            {
                clip.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == "OffsetSeconds" ||
                        e.PropertyName == "DurationSeconds")
                    {
                        OnClipPropertyChanged((Clip)s, e.PropertyName);
                    }
                };
            }
        }

        // ✅ Отписываемся от старых клипов
        private void UnsubscribeFromClips(IEnumerable<Clip> clips)
        {
            foreach (var clip in clips)
            {
                clip.PropertyChanged -= (s, e) =>
                {
                    if (e.PropertyName == "OffsetSeconds" ||
                        e.PropertyName == "DurationSeconds")
                    {
                        OnClipPropertyChanged((Clip)s, e.PropertyName);
                    }
                };
            }
        }

        public TrackRenderService(IClipRenderService clipRenderService)
        {
            _clipRenderService = clipRenderService;
        }

        public void RenderTracks(Canvas canvas, IEnumerable<Track> tracks, double canvasWidth, double timelineScale)
        {
            UnsubscribeFromClips(
                from item in canvas.Children.OfType<UIElement>()
                let f = item as FrameworkElement
                where f != null && f.Tag is Clip clip
                select (Clip)f.Tag
            );
            // Рисуем треки
            int trackIndex = 0;
            foreach (var track in tracks)
            {
                double trackY = trackIndex * TrackHeight;
                DrawTrackBackground(canvas, trackY, canvasWidth);
                _clipRenderService.RenderClips(canvas, track.Clips, trackY, timelineScale);
                trackIndex++;
            }

            // Подписываемся на новые клипы
            SubscribeToClips(tracks.SelectMany(t => t.Clips));
        }

        public void ClearTracks(Canvas canvas)
        {
            var toRemove = canvas.Children.OfType<UIElement>()
                .Where(e =>
                {
                    int zIndex = Canvas.GetZIndex(e);
                    if (zIndex > 1000) return false;

                    double top = Canvas.GetTop(e);
                    return !double.IsNaN(top) && top >= 0;
                })
                .ToList();

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
// Сервис отрисовки треков timeline (Canvas).
// Рендерит заголовки треков (видео=темно-синий, аудио=темно-зеленый) + клипы.
// TrackHeight=50px, TrackHeaderHeight=25px, TrackGap=5px.
// Иконки: 🎬 видео, 🎵 аудио. Очищает область треков (>=25px).

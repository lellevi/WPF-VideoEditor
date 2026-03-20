using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VideoEditorWPF.Services
{
    public interface ITimelineRenderService
    {
        void RenderTimeline(Canvas canvas, double scale, double viewportWidth);
        void ClearTimeline(Canvas canvas);
    }

    public class TimelineRenderService : ITimelineRenderService
    {
        public const double TrackHeaderHeight = 25;

        public void RenderTimeline(Canvas canvas, double scale, double viewportWidth)
        {
            if (canvas.ActualWidth <= 0) return;

            DrawTimelineRuler(canvas, scale, viewportWidth);
        }

        public void ClearTimeline(Canvas canvas)
        {
            var toRemove = canvas.Children.OfType<UIElement>()
                .Where(e =>
                {
                    double top = Canvas.GetTop(e);
                    return !double.IsNaN(top) && top < TrackHeaderHeight;
                })
                .ToList();

            foreach (var elem in toRemove)
            {
                canvas.Children.Remove(elem);
            }
        }

        private void DrawTimelineRuler(Canvas canvas, double pixelsPerSecond, double viewportWidth)
        {
            double totalSeconds = Math.Min(viewportWidth / pixelsPerSecond * 2, 3600);

            var intervals = GetRulerIntervals(pixelsPerSecond);

            DrawMajorTicks(canvas, pixelsPerSecond, totalSeconds, intervals.majorInterval);
            DrawMinorTicks(canvas, pixelsPerSecond, totalSeconds, intervals.majorInterval, intervals.minorInterval);
        }

        private void DrawMajorTicks(Canvas canvas, double pixelsPerSecond, double totalSeconds, double majorInterval)
        {
            for (double sec = 0; sec <= totalSeconds; sec += majorInterval)
            {
                double x = sec * pixelsPerSecond;

                var majorLine = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 20,
                    Stroke = Brushes.WhiteSmoke,
                    StrokeThickness = 2
                };
                Canvas.SetTop(majorLine, 0);
                canvas.Children.Add(majorLine);

                var label = new TextBlock
                {
                    Text = FormatTimeText(sec),
                    Foreground = Brushes.WhiteSmoke,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold
                };
                Canvas.SetLeft(label, x + 3);
                Canvas.SetTop(label, 1);
                canvas.Children.Add(label);
            }
        }

        private void DrawMinorTicks(Canvas canvas, double pixelsPerSecond, double totalSeconds, double majorInterval, double minorInterval)
        {
            if (minorInterval <= 0 || minorInterval >= majorInterval) return;

            for (double sec = 0; sec <= totalSeconds; sec += minorInterval)
            {
                if (Math.Abs(sec % majorInterval) < 0.001) continue;

                double x = sec * pixelsPerSecond;

                var minorLine = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = 12,
                    Stroke = Brushes.LightGray,
                    StrokeThickness = 1
                };
                Canvas.SetTop(minorLine, 0);
                canvas.Children.Add(minorLine);
            }
        }

        private (double majorInterval, double minorInterval) GetRulerIntervals(double pixelsPerSecond)
        {
            return pixelsPerSecond switch
            {
                < 2 => (120, 60),
                < 5 => (60, 30),
                < 10 => (30, 10),
                < 20 => (10, 5),
                < 40 => (5, 1),
                < 80 => (2, 0.5),
                < 160 => (1, 0.5),
                < 320 => (1, 0.2),
                _ => (0.5, 0.1)
            };
        }

        private string FormatTimeText(double seconds)
        {
            return seconds switch
            {
                >= 3600 => $"{TimeSpan.FromSeconds(seconds):h\\:mm\\:ss}s",
                >= 60 => $"{TimeSpan.FromSeconds(seconds):mm\\:ss}s",
                >= 1 => $"{TimeSpan.FromSeconds(seconds):mm\\:ss}s",
                _ => $"{TimeSpan.FromSeconds(seconds):mm\\:ss\\.ff}s",
            };
        }
    }
}
// Сервис отрисовки линейки timeline (Canvas).
// Адаптивные тики: major(секунды) + minor по pixelsPerSecond.
// Форматирование времени (h:mm:ss / mm:ss / ss.ff).
// TrackHeaderHeight=25px. Очищает только верхнюю область (<25px).

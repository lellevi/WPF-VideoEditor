using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VideoEditorWPF.Services
{
    public interface ISnapIndicatorService
    {
        void Show(Canvas canvas, double position, double scale, double height);
        void Hide();
    }

    public class SnapIndicatorService : ISnapIndicatorService
    {
        private Line _line;
        private Border _border;
        private TextBlock _label;
        private Canvas _currentCanvas;

        public void Show(Canvas canvas, double position, double scale, double height)
        {
            if (_currentCanvas == null)
            {
                _currentCanvas = canvas;
            }

            double timeInSeconds = position / scale;

            // Create or update the snap line
            if (_line == null)
            {
                _line = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                Canvas.SetZIndex(_line, 1001);
                canvas.Children.Add(_line);
            }

            _line.X1 = position;
            _line.X2 = position;
            _line.Y1 = 0;
            _line.Y2 = height;
            _line.Visibility = Visibility.Visible;

            // Create or update the snap label
            if (_border == null)
            {
                _label = new TextBlock
                {
                    Foreground = Brushes.Black,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                _border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 215, 0)), // Gold
                    CornerRadius = new CornerRadius(3),
                    Child = _label,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(218, 165, 32)), // GoldenRod
                    BorderThickness = new Thickness(1)
                };
                Canvas.SetZIndex(_border, 1002);
                canvas.Children.Add(_border);
            }

            // Format time as mm:ss.f
            var timeSpan = TimeSpan.FromSeconds(timeInSeconds);
            string timeText = timeInSeconds >= 60
                ? $"{timeSpan:mm\\:ss\\.f}"
                : $"{timeSpan:ss\\.f}s";

            _label.Text = timeText;

            // Position label above the line
            Canvas.SetLeft(_border, position + 5);
            Canvas.SetTop(_border, 5);
            _border.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            if (_line != null)
                _line.Visibility = Visibility.Collapsed;

            if (_border != null)
                _border.Visibility = Visibility.Collapsed;
        }
    }
}

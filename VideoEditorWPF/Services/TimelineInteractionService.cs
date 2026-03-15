using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public interface ITimelineInteractionService
    {
        ClipDragInfo StartDrag(Point position, Canvas canvas);
        void UpdateDrag(Point currentPosition, ClipDragInfo dragInfo, Canvas canvas, TimelineViewModel timeline, bool isShiftPressed);
        void FinishDrag(ClipDragInfo dragInfo);
        void MovePlayhead(Point position, TimelineViewModel timeline, Rectangle playhead, bool isShiftPressed);
        double SnapToGrid(double position, bool snap, double scale);
    }

    public class TimelineInteractionService : ITimelineInteractionService
    {
        private readonly ISnapIndicatorService _snapIndicator;

        public TimelineInteractionService(ISnapIndicatorService snapIndicator)
        {
            _snapIndicator = snapIndicator;
        }

        public ClipDragInfo StartDrag(Point position, Canvas canvas)
        {
            var hitElement = canvas.InputHitTest(position) as DependencyObject;

            while (hitElement != null && hitElement != canvas)
            {
                if (hitElement is Rectangle rect && rect.Tag is Clip clip)
                {
                    var label = canvas.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Tag == clip);

                    return new ClipDragInfo
                    {
                        Clip = clip,
                        Visual = rect,
                        Label = label,
                        LastPosition = position
                    };
                }
                hitElement = LogicalTreeHelper.GetParent(hitElement);
            }

            return null;
        }

        public void UpdateDrag(Point currentPosition, ClipDragInfo dragInfo, Canvas canvas, TimelineViewModel timeline, bool isShiftPressed)
        {
            if (dragInfo?.Clip == null) return;

            // Get current visual X position
            double visualStartX = dragInfo.Visual != null
                ? Canvas.GetLeft(dragInfo.Visual)
                : dragInfo.Clip.GetOffsetPixels(timeline.TimelineScale);

            // Calculate delta
            double deltaX = currentPosition.X - dragInfo.LastPosition.X;

            // Apply delta
            double newStartX = Math.Max(0, visualStartX + deltaX);

            // Snap to grid if Shift is pressed
            if (isShiftPressed)
            {
                newStartX = SnapToGrid(newStartX, true, timeline.TimelineScale);
                _snapIndicator.Show(canvas, newStartX, timeline.TimelineScale, timeline.CalculatedHeight);
            }
            else
            {
                _snapIndicator.Hide();
            }

            // Update model
            timeline.UpdateClipTimePosition(dragInfo.Clip, newStartX);

            // Update visuals
            if (dragInfo.Visual != null)
            {
                Canvas.SetLeft(dragInfo.Visual, newStartX);
            }

            if (dragInfo.Label != null)
            {
                Canvas.SetLeft(dragInfo.Label, newStartX + 5);
            }

            dragInfo.LastPosition = currentPosition;
        }

        public void FinishDrag(ClipDragInfo dragInfo)
        {
            _snapIndicator.Hide();
            // Cleanup handled by caller (RefreshTracks)
        }

        public void MovePlayhead(Point position, TimelineViewModel timeline, Rectangle playhead, bool isShiftPressed)
        {
            double playheadPosition = SnapToGrid(position.X, isShiftPressed, timeline.TimelineScale);

            Canvas.SetLeft(playhead, playheadPosition);
            timeline.PlayheadPosition = playheadPosition;
        }

        public double SnapToGrid(double position, bool snap, double scale)
        {
            if (!snap)
                return position;

            const double snapInterval = 0.5; // 0.5 seconds

            // Convert pixels to seconds
            double timeInSeconds = position / scale;

            // Round to nearest 0.5 second interval
            double snappedSeconds = Math.Round(timeInSeconds / snapInterval) * snapInterval;

            // Convert back to pixels
            return snappedSeconds * scale;
        }
    }
}

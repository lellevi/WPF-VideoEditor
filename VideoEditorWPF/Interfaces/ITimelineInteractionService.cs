using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Interfaces
{
    public interface ITimelineInteractionService
    {
        ClipDragInfo StartDrag(Point position, Canvas canvas);
        ClipDragInfo StartDrag(Point position, Canvas canvas, TimelineViewModel timeline);
        void UpdateDrag(Point currentPosition, ClipDragInfo dragInfo, Canvas canvas, TimelineViewModel timeline, bool isShiftPressed);
        void FinishDrag(ClipDragInfo dragInfo);
        void MovePlayhead(Point position, TimelineViewModel timeline, Rectangle playhead, bool isShiftPressed);
        double SnapToGrid(double position, bool snap, double scale);
    }
}

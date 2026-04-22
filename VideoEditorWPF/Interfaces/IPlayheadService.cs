using System.Windows.Controls;
using System.Windows.Shapes;

namespace VideoEditorWPF.Interfaces
{
    public interface IPlayheadService
    {
        void UpdateRulerPlayhead(Canvas rulerCanvas, double position);
        void UpdateTimelinePlayhead(Rectangle playhead, double position);
        void SyncPlayheads(Canvas rulerCanvas, Rectangle timelinePlayhead, double position);
    }
}

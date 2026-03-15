using System.Linq;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace VideoEditorWPF.Services
{
    public interface IPlayheadService
    {
        void UpdateRulerPlayhead(Canvas rulerCanvas, double position);
        void UpdateTimelinePlayhead(Rectangle playhead, double position);
        void SyncPlayheads(Canvas rulerCanvas, Rectangle timelinePlayhead, double position);
    }

    public class PlayheadService : IPlayheadService
    {
        public void UpdateRulerPlayhead(Canvas rulerCanvas, double position)
        {
            var playhead = rulerCanvas.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name == "RulerPlayhead");

            if (playhead != null)
            {
                Canvas.SetLeft(playhead, position);
            }
        }

        public void UpdateTimelinePlayhead(Rectangle playhead, double position)
        {
            if (playhead != null)
            {
                Canvas.SetLeft(playhead, position);
            }
        }

        public void SyncPlayheads(Canvas rulerCanvas, Rectangle timelinePlayhead, double position)
        {
            UpdateRulerPlayhead(rulerCanvas, position);
            UpdateTimelinePlayhead(timelinePlayhead, position);
        }
    }
}

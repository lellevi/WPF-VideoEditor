using System.Windows.Controls;

namespace VideoEditorWPF.Interfaces
{
    public interface ITimelineRenderService
    {
        void RenderTimeline(Canvas canvas, double scale, double viewportWidth, double timelineLength);
        void ClearTimeline(Canvas canvas);
    }
}

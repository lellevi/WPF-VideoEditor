using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public interface ITimelineService
    {
        void AddClipToTimeline(MediaFile mediaFile, TimelineViewModel timeline);
    }

    public class TimelineService : ITimelineService
    {
        public void AddClipToTimeline(MediaFile mediaFile, TimelineViewModel timeline)
        {
            timeline.AddClipToTrack(mediaFile);
        }
    }
}

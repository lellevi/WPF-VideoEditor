using System;
using System.Linq;
using System.Threading.Tasks;
using VideoEditorWPF.Interfaces;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public class TimelineService : ITimelineService
    {
        private const double DefaultTimelineLengthSeconds = 30.0;
        public async Task AddClipToTimelineAsync(MediaFile mediaFile, TimelineViewModel timeline)
        {
            await Task.Run(() => timeline.AddClipToTrack(mediaFile));
        }
        public double GetTimelineLength(TimelineViewModel timeline)
        {
            double maxEndTime = DefaultTimelineLengthSeconds;

            foreach (var track in timeline.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    double clipEndTime = clip.OffsetSeconds + clip.DurationSeconds;
                    if (clipEndTime > maxEndTime)
                        maxEndTime = clipEndTime;
                }
            }

            return Math.Max(DefaultTimelineLengthSeconds, maxEndTime);
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public interface ITimelineService
    {
        Task AddClipToTimelineAsync(MediaFile mediaFile, TimelineViewModel timeline);
        double GetTimelineLength(TimelineViewModel timeline);
    }

    public class TimelineService : ITimelineService
    {
        private const double DefaultTimelineLengthSeconds = 30.0;

        /// <summary>
        /// Добавляет клип на таймлайн
        /// </summary>
        public async Task AddClipToTimelineAsync(MediaFile mediaFile, TimelineViewModel timeline)
        {
            await Task.Run(() => timeline.AddClipToTrack(mediaFile));
        }

        /// <summary>
        /// Вычисляет общую длину таймлайна (max end всех клипов)
        /// </summary>
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

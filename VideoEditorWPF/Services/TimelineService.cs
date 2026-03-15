using System;
using VideoEditorWPF.Models;
using VideoEditorWPF.ViewModels;

namespace VideoEditorWPF.Services
{
    public interface ITimelineService
    {
        void AddClipToTimeline(MediaFile mediaFile, TimelineViewModel timeline);
        double GetTimelineLength(TimelineViewModel timeline);
    }

    public class TimelineService : ITimelineService
    {
        private const double DefaultTimelineLengthSeconds = 30.0;

        public void AddClipToTimeline(MediaFile mediaFile, TimelineViewModel timeline)
        {
            timeline.AddClipToTrack(mediaFile);

            // Adjust timeline length if needed
            AdjustTimelineLength(timeline, mediaFile);
        }

        /// <summary>
        /// Adjusts the timeline length based on the added media clip.
        /// If the clip extends beyond the current timeline length, expand the timeline.
        /// </summary>
        private void AdjustTimelineLength(TimelineViewModel timeline, MediaFile mediaFile)
        {
            // Calculate the required timeline length based on all clips
            double requiredLength = CalculateRequiredTimelineLength(timeline);

            // Ensure minimum default length
            double newLength = Math.Max(DefaultTimelineLengthSeconds, requiredLength);

            // TODO: Future enhancement - Store and update TimelineLength property in TimelineViewModel
            // For now, this logic prepares the foundation for dynamic timeline scaling
            // Future implementation could:
            // 1. Add a TimelineLength property to TimelineViewModel
            // 2. Update UI elements (ruler, grid) to reflect this length
            // 3. Implement auto-expansion with padding (e.g., +5 seconds beyond last clip)
            // 4. Add user preference for manual vs auto-expansion
            // 5. Implement timeline shrinking when clips are removed (optional)
        }

        /// <summary>
        /// Calculates the minimum timeline length required to display all clips.
        /// </summary>
        private double CalculateRequiredTimelineLength(TimelineViewModel timeline)
        {
            double maxEndTime = DefaultTimelineLengthSeconds;

            foreach (var track in timeline.Tracks)
            {
                foreach (var clip in track.Clips)
                {
                    double clipEndTime = clip.OffsetSeconds + clip.DurationSeconds;
                    if (clipEndTime > maxEndTime)
                    {
                        maxEndTime = clipEndTime;
                    }
                }
            }

            return maxEndTime;
        }

        /// <summary>
        /// Gets the current effective timeline length based on clips or default.
        /// </summary>
        public double GetTimelineLength(TimelineViewModel timeline)
        {
            return Math.Max(DefaultTimelineLengthSeconds, CalculateRequiredTimelineLength(timeline));
        }
    }
}

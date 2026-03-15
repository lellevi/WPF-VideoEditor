using System;
using VideoEditorWPF.Models;

namespace VideoEditorWPF.Factories
{
    public interface IClipFactory
    {
        Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex);
    }

    public class ClipFactory : IClipFactory
    {
        public Clip CreateClip(MediaFile mediaFile, double startTimeSeconds, double timelineScale, int trackIndex)
        {
            bool isVideo = !mediaFile.FilePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                           !mediaFile.FilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

            return new Clip
            {
                FilePath = mediaFile.FilePath,
                OffsetSeconds = startTimeSeconds,
                OffsetPixels = startTimeSeconds * timelineScale,
                DurationSeconds = mediaFile.Duration.TotalSeconds,
                Width = mediaFile.Duration.TotalSeconds * timelineScale,
                IsVideoClip = isVideo,
                TrackIndex = trackIndex
            };
        }
    }
}

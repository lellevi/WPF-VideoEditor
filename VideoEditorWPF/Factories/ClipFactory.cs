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
                StartTimeSeconds = startTimeSeconds,
                StartX = startTimeSeconds * timelineScale,
                DurationSeconds = mediaFile.Duration.TotalSeconds,
                Width = mediaFile.Duration.TotalSeconds * timelineScale,
                IsVideoClip = isVideo,
                TrackIndex = trackIndex
            };
        }
    }
}
// Фабрика для создания клипов видеоредактора.
// Преобразует MediaFile в Clip с расчетом позиции (StartX) и ширины (Width) по timelineScale.
// Автоматически определяет тип клипа (видео/аудио) по расширению файла.
// Устанавливает начальную позицию, длительность и трек для timeline.
